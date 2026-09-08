using System.Globalization;
using System.IO;
using System.Text;

namespace EolTester.App.Services;

/// <summary>
/// Log chống trùng barcode cho SCAN MODE — hoàn toàn độc lập với file CSV kết quả (tùy chọn, cột do Admin tự
/// cấu hình tự do qua <see cref="Configuration.Models.CsvExportSettings.ColumnSpecs"/>). Trước đây dedup dựa
/// vào việc dò cột &lt;barcode&gt; trong file CSV "đang hoạt động" — coupling này có 2 vấn đề: (1) nếu Admin
/// chưa cấu hình/đã tắt thư mục xuất CSV, dedup lặng lẽ luôn pass (mất hẳn bảo vệ chống trùng, không cảnh báo
/// gì); (2) đổi cấu hình cột giữa ngày làm CSV tách sang file <c>-n</c> khác, dedup chỉ còn thấy đúng file
/// "đang hoạt động" hiện tại. Ở đây ghi thẳng ra <c>barcode-&lt;yyyyMMdd&gt;.log</c> (plain text, mỗi dòng 1
/// barcode, không phụ thuộc cấu hình cột nào) trong <see cref="AppPaths.ScanLogDirectory"/>.
/// <para>
/// Thời điểm ghi nhận GIỮ NGUYÊN như thiết kế dedup cũ: lúc PLC báo <c>SIGNAL_CSV_WRITE</c> (1 chu trình test
/// đã hoàn tất), không phải ngay lúc operator vừa quét — barcode của 1 chu trình chưa hoàn tất (máy dừng giữa
/// chừng, lỗi, PLC không bắn tín hiệu) sẽ KHÔNG bị coi là đã dùng, vẫn quét lại được cùng ngày.
/// </para>
/// </summary>
public sealed class BarcodeScanLogService : IBarcodeScanLogService
{
    /// <summary>Số file tối đa giữ lại (~3 tháng, mỗi ngày 1 file) — vượt quá thì tự xóa các file cũ nhất.</summary>
    private const int MaxLogFiles = 100;

    private const string FilePrefix = "barcode-";
    private const string FileExtension = ".log";
    private const string DateFormat = "yyyyMMdd";

    private readonly string _directory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private DateOnly _cachedDate;
    private HashSet<string>? _cachedBarcodes;

    public BarcodeScanLogService(string? directory = null)
    {
        _directory = directory ?? AppPaths.ScanLogDirectory;
    }

    public async Task<bool> IsRecordedTodayAsync(string barcode, CancellationToken ct = default)
    {
        var trimmed = barcode.Trim();
        await _lock.WaitAsync(ct);
        try
        {
            await EnsureCacheForTodayAsync(ct);
            return _cachedBarcodes!.Contains(trimmed);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RecordAsync(string barcode, CancellationToken ct = default)
    {
        var trimmed = barcode.Trim();
        await _lock.WaitAsync(ct);
        try
        {
            await EnsureCacheForTodayAsync(ct);
            Directory.CreateDirectory(_directory);
            await File.AppendAllLinesAsync(CurrentLogFilePath(), [trimmed], Encoding.UTF8, ct);
            _cachedBarcodes!.Add(trimmed);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Nạp lại cache nếu sang ngày mới; đồng thời chạy dọn file quá hạn — CHỈ 1 lần đúng lúc sang ngày
    /// (không quét thư mục mỗi lần gọi), vì cả 2 việc đều chỉ cần làm lại khi ngày đổi.</summary>
    private async Task EnsureCacheForTodayAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (_cachedBarcodes is not null && _cachedDate == today) return;

        var barcodes = new HashSet<string>();
        var path = LogFilePath(today);
        if (File.Exists(path))
        {
            foreach (var line in await File.ReadAllLinesAsync(path, Encoding.UTF8, ct))
            {
                if (!string.IsNullOrWhiteSpace(line)) barcodes.Add(line.Trim());
            }
        }

        _cachedDate = today;
        _cachedBarcodes = barcodes;

        CleanupOldLogFiles();
    }

    private string CurrentLogFilePath() => LogFilePath(_cachedDate);

    private string LogFilePath(DateOnly date) => Path.Combine(_directory, $"{FilePrefix}{date.ToString(DateFormat, CultureInfo.InvariantCulture)}{FileExtension}");

    /// <summary>Giữ tối đa <see cref="MaxLogFiles"/> file, xóa các file cũ nhất theo ngày trong tên file (không
    /// phải theo thời gian sửa đổi trên đĩa — đáng tin cậy hơn nếu file bị copy/backup làm đổi timestamp).</summary>
    private void CleanupOldLogFiles()
    {
        if (!Directory.Exists(_directory)) return;

        var files = Directory.GetFiles(_directory, $"{FilePrefix}*{FileExtension}")
            .Select(path => (Path: path, Date: TryParseDateFromFileName(path)))
            .Where(f => f.Date is not null)
            .OrderBy(f => f.Date)
            .ToList();

        var excessCount = files.Count - MaxLogFiles;
        for (var i = 0; i < excessCount; i++)
        {
            try
            {
                File.Delete(files[i].Path);
            }
            catch (IOException)
            {
                // File cũ đang bị chương trình khác mở (VD Admin đang xem) — bỏ qua, dọn lại vào lần sang ngày kế tiếp.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static DateOnly? TryParseDateFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith(FilePrefix, StringComparison.Ordinal)) return null;

        var datePart = name[FilePrefix.Length..];
        return DateOnly.TryParseExact(datePart, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}
