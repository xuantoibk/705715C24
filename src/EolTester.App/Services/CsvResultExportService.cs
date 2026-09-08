using System.IO;
using System.Text;
using EolTester.Communication;
using EolTester.Configuration;
using EolTester.Configuration.Models;
using EolTester.Core;

namespace EolTester.App.Services;

/// <summary>
/// Ghi kết quả kiểm tra ra file CSV theo ngày. Cột do Admin cấu hình tự do qua
/// <see cref="Configuration.Models.CsvExportSettings.ColumnSpecs"/> (xem <see cref="CsvColumnSpecParser"/>):
/// token đặc biệt (&lt;STT&gt;/&lt;barcode&gt;/&lt;JOB&gt;/&lt;date&gt;/&lt;time&gt;/composite ngày-giờ) hoặc
/// địa chỉ thanh ghi ("Dxxxx"/"Dxxxx.b", tiêu đề cột tra theo cột "Heading" trong spec-register-map.csv, giá
/// trị diễn giải "OK"/"NG" thay vì số thô nếu cột "Type"="OKNG"). Cột Barcode LUÔN có mặt (tự chèn đầu danh
/// sách nếu Admin không tự gõ token &lt;barcode&gt;).
/// <para>
/// Khi cấu hình cột đổi giữa các lần ghi cùng ngày (hoặc file `&lt;ngày&gt;.csv` đã có từ trước với header
/// khác), tự tách sang file `&lt;ngày&gt;-1.csv`, `-2.csv`... — xem <see cref="ResolveTargetFileAsync"/>: dò
/// tuần tự, dùng file đầu tiên chưa tồn tại hoặc có header khớp đúng cấu hình hiện tại, không bao giờ ghi đè
/// lên 1 file có cấu trúc cột khác.
/// </para>
/// Trigger gọi <see cref="AppendRowAsync"/> là <see cref="PlcPollingService.CsvWriteSignalRaised"/> (PLC tự
/// báo qua 1 bit riêng, xem ShellViewModel) — không tự polling/tự quyết định thời điểm ghi.
/// </summary>
public sealed class CsvResultExportService : ICsvResultExportService
{
    private const int MaxWriteAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(300);

    private readonly ICsvExportSettingsStore _settingsStore;
    private readonly ISpecRegisterMapSource _registerMapSource;
    private readonly PlcRegisterImage _registerTable;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>Cache cho file CSV "đang hoạt động" hiện tại (đã resolve qua <see cref="ResolveTargetFileAsync"/>)
    /// — đếm số dòng cho cột &lt;STT&gt;, tránh phải đọc lại cả file mỗi lần dùng. Nạp lại toàn bộ khi file
    /// resolve ra khác đường dẫn đã cache (đổi ngày HOẶC đổi cấu hình cột giữa ngày khiến resolve ra file `-n`
    /// khác). Luôn truy cập trong <see cref="_writeLock"/>.
    /// <para>
    /// KHÔNG còn dùng để chống trùng barcode (SCAN MODE) — trách nhiệm đó đã chuyển sang
    /// <see cref="IBarcodeScanLogService"/> (scanlog riêng, độc lập với cấu hình cột CSV, xem ShellViewModel).
    /// </para>
    /// </summary>
    private string? _cachedFilePath;
    private int _cachedRowCount;

    public CsvResultExportService(ICsvExportSettingsStore settingsStore, ISpecRegisterMapSource registerMapSource, PlcRegisterImage registerTable)
    {
        _settingsStore = settingsStore;
        _registerMapSource = registerMapSource;
        _registerTable = registerTable;
    }

    public async Task AppendRowAsync(string barcode, string jobId, CancellationToken ct = default)
    {
        var settings = await _settingsStore.LoadAsync(ct);
        var outputDirectory = AppPaths.ResolveCsvExportDirectory(settings.OutputDirectory);

        Directory.CreateDirectory(outputDirectory);
        var now = DateTime.Now;

        var effectiveSpecs = BuildEffectiveSpecs(settings.ColumnSpecs);
        var headingsByAddress = await BuildAddressToHeadingMapAsync(ct);
        var typesByAddress = await BuildAddressToTypeMapAsync(ct);
        var headerFields = BuildHeaderFields(effectiveSpecs, headingsByAddress);

        // Khóa (SemaphoreSlim cấp instance, service là Singleton) để resolve file + check cache + ghi là 1
        // thao tác atomic giữa các lần gọi — nếu không, 2 lần trigger SIGNAL_CSV_WRITE đủ gần nhau (2 sản
        // phẩm test liên tiếp nhanh) có thể chồng lấn: hoặc cả 2 đều thấy "chưa có file" nên cùng ghi
        // header/BOM (hỏng cấu trúc CSV), hoặc luồng thứ 2 mở file trong lúc luồng đầu đang giữ FileShare.Read
        // sẽ ném IOException (sharing violation) và mất hẳn dòng kết quả đó. Khóa này CHỈ có hiệu lực giữa các
        // lệnh ghi của chính app — không giúp được gì nếu 1 chương trình KHÁC (VD Excel) đang mở cùng file,
        // xem WriteRowWithRetryAsync bên dưới để xử lý riêng trường hợp đó.
        await _writeLock.WaitAsync(ct);
        try
        {
            var (filePath, isNewFile) = await ResolveTargetFileAsync(outputDirectory, now, headerFields, ct);
            await EnsureFileCacheLoadedAsync(filePath, ct);

            var sequenceNumber = _cachedRowCount + 1;
            var rowFields = effectiveSpecs
                .Select(spec => ResolveFieldValue(spec, now, barcode, jobId, sequenceNumber, typesByAddress))
                .ToList();

            await WriteRowWithRetryAsync(filePath, isNewFile, headerFields, rowFields, ct);

            _cachedRowCount++;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Chèn <see cref="CsvColumnSpec.BarcodeColumn"/> vào đầu danh sách nếu Admin chưa tự gõ token
    /// &lt;barcode&gt; ở đâu đó — cột Barcode luôn phải có mặt (đã chốt với người dùng), vị trí mặc định là
    /// đầu tiên, nhưng Admin có thể tự dời bằng cách gõ tường minh.</summary>
    private static List<CsvColumnSpec> BuildEffectiveSpecs(IReadOnlyList<string> rawTokens)
    {
        var specs = rawTokens.Select(CsvColumnSpecParser.Parse).ToList();
        if (!specs.Any(s => s is CsvColumnSpec.BarcodeColumn))
        {
            specs.Insert(0, new CsvColumnSpec.BarcodeColumn());
        }
        return specs;
    }

    private static List<string> BuildHeaderFields(IReadOnlyList<CsvColumnSpec> specs, IReadOnlyDictionary<ModbusWordAddress, string> headingsByAddress) =>
        specs.Select(spec => spec switch
        {
            CsvColumnSpec.Sequence => "STT",
            CsvColumnSpec.BarcodeColumn => "Barcode",
            CsvColumnSpec.JobIdColumn => "JOB ID",
            CsvColumnSpec.DateTimeColumn { IsTime: true } => "Time",
            CsvColumnSpec.DateTimeColumn => "Date",
            CsvColumnSpec.RegisterColumn register => ResolveRegisterHeader(register.RawText, headingsByAddress),
            _ => throw new NotSupportedException($"Loại cột CSV chưa hỗ trợ: {spec.GetType().Name}"),
        }).ToList();

    private static string ResolveRegisterHeader(string rawText, IReadOnlyDictionary<ModbusWordAddress, string> headingsByAddress)
    {
        if (ModbusWordAddress.TryParse(rawText, out var parsed) && headingsByAddress.TryGetValue(parsed, out var heading))
            return heading;
        return rawText; // Không khớp Heading nào (VD đã bị xóa khỏi spec-register-map.csv sau khi lưu) — dùng thẳng địa chỉ.
    }

    private string ResolveFieldValue(CsvColumnSpec spec, DateTime now, string barcode, string jobId, int sequenceNumber, IReadOnlyDictionary<ModbusWordAddress, string> typesByAddress) =>
        spec switch
        {
            CsvColumnSpec.Sequence => sequenceNumber.ToString(),
            CsvColumnSpec.BarcodeColumn => barcode,
            CsvColumnSpec.JobIdColumn => jobId,
            CsvColumnSpec.DateTimeColumn dt => FormatDateTime(now, dt),
            CsvColumnSpec.RegisterColumn register => ResolveRegisterValue(register.RawText, typesByAddress),
            _ => throw new NotSupportedException($"Loại cột CSV chưa hỗ trợ: {spec.GetType().Name}"),
        };

    private static string FormatDateTime(DateTime now, CsvColumnSpec.DateTimeColumn spec)
    {
        var format = spec.CustomFormat ?? (spec.IsTime ? "T" : "d");
        try
        {
            return now.ToString(format);
        }
        catch (FormatException)
        {
            // Composite format lỗi (VD Admin gõ sai sub-token trước khi validate kịp bắt) — để trống thay vì
            // chặn cả dòng ghi kết quả.
            return string.Empty;
        }
    }

    /// <summary>Đọc giá trị 1 thanh ghi từ cache PLC, diễn giải "OK"/"NG" thay vì số thô nếu Type="OKNG" —
    /// word: 1=OK/2=NG; bit (Dxxxx.x): 1=OK/0=NG (đúng quy ước người dùng đã chốt). Type khác/không khai báo
    /// → ghi giá trị thô như cũ (KHÔNG áp Scale — Scale chỉ có ý nghĩa với giá trị đo lường số).</summary>
    private string ResolveRegisterValue(string rawText, IReadOnlyDictionary<ModbusWordAddress, string> typesByAddress)
    {
        if (!ModbusWordAddress.TryParse(rawText, out var parsed) || !_registerTable.TryGetValue(rawText, out var raw))
            return string.Empty;

        if (typesByAddress.TryGetValue(parsed, out var type) && string.Equals(type, "OKNG", StringComparison.OrdinalIgnoreCase))
        {
            if (parsed.BitIndex is not null) return raw == 1 ? "OK" : raw == 0 ? "NG" : string.Empty;
            return raw == 1 ? "OK" : raw == 2 ? "NG" : string.Empty;
        }

        return raw.ToString();
    }

    private async Task<Dictionary<ModbusWordAddress, string>> BuildAddressToHeadingMapAsync(CancellationToken ct)
    {
        var addressByKey = await _registerMapSource.LoadAsync(ct);
        var headingByKey = await _registerMapSource.LoadHeadingsAsync(ct);
        var map = new Dictionary<ModbusWordAddress, string>();
        foreach (var (key, heading) in headingByKey)
        {
            if (addressByKey.TryGetValue(key, out var address) && ModbusWordAddress.TryParse(address, out var parsed))
                map[parsed] = heading;
        }
        return map;
    }

    private async Task<Dictionary<ModbusWordAddress, string>> BuildAddressToTypeMapAsync(CancellationToken ct)
    {
        var addressByKey = await _registerMapSource.LoadAsync(ct);
        var typeByKey = await _registerMapSource.LoadTypesAsync(ct);
        var map = new Dictionary<ModbusWordAddress, string>();
        foreach (var (key, type) in typeByKey)
        {
            if (addressByKey.TryGetValue(key, out var address) && ModbusWordAddress.TryParse(address, out var parsed))
                map[parsed] = type;
        }
        return map;
    }

    /// <summary>
    /// Dò file đúng cấu hình cột hiện tại: <c>&lt;ngày&gt;.csv</c> → nếu chưa tồn tại thì dùng (file mới,
    /// header sẽ ghi theo cấu hình hiện tại); nếu tồn tại và dòng header khớp thì dùng lại; nếu khác thì thử
    /// <c>&lt;ngày&gt;-1.csv</c>, <c>-2.csv</c>... tăng dần tới khi gặp file chưa tồn tại hoặc header khớp —
    /// cho phép Admin bật/tắt qua lại 2 cấu hình trong ngày mà không sinh vô hạn file mới, đồng thời KHÔNG BAO
    /// GIỜ ghi đè lên 1 file có cấu trúc cột khác (kể cả file cũ từ trước khi có tính năng cột linh hoạt này).
    /// Chỉ đọc dòng đầu tiên của mỗi file ứng viên — không đọc cả file, chi phí không đáng kể nên không cần
    /// cache riêng, chạy lại mỗi lần ghi.
    /// </summary>
    private static async Task<(string FilePath, bool IsNewFile)> ResolveTargetFileAsync(string outputDirectory, DateTime now, IReadOnlyList<string> desiredHeaderFields, CancellationToken ct)
    {
        var desiredHeaderLine = string.Join(",", desiredHeaderFields.Select(EscapeField));
        var baseName = now.ToString("yyyyMMdd");

        for (var index = 0; ; index++)
        {
            var fileName = index == 0 ? $"{baseName}.csv" : $"{baseName}-{index}.csv";
            var candidatePath = Path.Combine(outputDirectory, fileName);

            if (!File.Exists(candidatePath)) return (candidatePath, true);

            var existingHeader = await ReadFirstLineAsync(candidatePath, ct);
            if (existingHeader == desiredHeaderLine) return (candidatePath, false);
        }
    }

    private static async Task<string?> ReadFirstLineAsync(string path, CancellationToken ct)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadLineAsync(ct);
    }

    /// <summary>Nạp lại cache (đếm dòng cho &lt;STT&gt;) nếu file resolve ra khác file đã cache — chỉ cần đếm
    /// số dòng dữ liệu (bỏ header), không cần parse từng cột. File chưa tồn tại (chưa có kết quả nào ghi vào
    /// file này) → đếm 0, không lỗi.</summary>
    private async Task EnsureFileCacheLoadedAsync(string filePath, CancellationToken ct)
    {
        if (_cachedFilePath == filePath) return;

        var rowCount = 0;
        if (File.Exists(filePath))
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var isHeader = true;
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (isHeader) { isHeader = false; continue; }
                if (string.IsNullOrWhiteSpace(line)) continue;
                rowCount++;
            }
        }

        _cachedFilePath = filePath;
        _cachedRowCount = rowCount;
    }

    /// <summary>
    /// Thử ghi 1 dòng, tự thử lại vài lần nếu mở file thất bại do <see cref="IOException"/> (sharing violation) —
    /// tình huống thực tế: 1 chương trình khác (VD Excel) đang mở file CSV của ngày hôm đó để xem trong lúc máy
    /// vẫn chạy. Phần lớn các khóa kiểu này chỉ tồn tại trong chốc lát (VD Excel đang refresh/lưu), nên thử lại
    /// sau 1 khoảng ngắn có cơ hội vượt qua mà không cần cơ chế hàng đợi phức tạp. Hết số lần thử vẫn lỗi thì để
    /// exception bay ra ngoài như cũ — <see cref="ShellViewModel.AppendCsvRowSafeAsync"/> sẽ bắt, log, và báo
    /// operator qua Notification history.
    /// </summary>
    private static async Task WriteRowWithRetryAsync(string filePath, bool isNewFile, IReadOnlyList<string> headerFields, IReadOnlyList<string> rowFields, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await WriteRowOnceAsync(filePath, isNewFile, headerFields, rowFields, ct);
                return;
            }
            catch (IOException) when (attempt < MaxWriteAttempts)
            {
                await Task.Delay(RetryDelay, ct);
            }
        }
    }

    private static async Task WriteRowOnceAsync(string filePath, bool isNewFile, IReadOnlyList<string> headerFields, IReadOnlyList<string> rowFields, CancellationToken ct)
    {
        // Tự viết BOM 1 lần bằng tay (chỉ khi file mới) thay vì dựa vào StreamWriter tự phát hiện preamble —
        // đảm bảo không ghi lặp BOM giữa file khi append vào file đã có từ trước (VD app khởi động lại cùng
        // ngày, file đã tồn tại từ lần chạy trước).
        await using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        if (isNewFile)
        {
            var bom = Encoding.UTF8.GetPreamble();
            await stream.WriteAsync(bom, ct);
        }

        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (isNewFile)
        {
            await writer.WriteLineAsync(string.Join(",", headerFields.Select(EscapeField)));
        }

        await writer.WriteLineAsync(string.Join(",", rowFields.Select(EscapeField)));
    }

    private static string EscapeField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        return field;
    }
}
