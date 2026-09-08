using System.IO;

namespace EolTester.App.Services;

public static class AppPaths
{
    /// <summary>
    /// Thư mục gốc chứa cấu hình + log của ứng dụng. Ưu tiên CẠNH file .exe đang chạy
    /// (<see cref="AppContext.BaseDirectory"/>) để bảo trì tại hiện trường dễ dàng — copy nguyên thư mục app
    /// là mang theo toàn bộ cấu hình. Nếu thư mục exe KHÔNG ghi được (VD cài trong <c>C:\Program Files</c>,
    /// UAC chặn ghi) thì tự rơi về <c>%LocalAppData%\EolTester</c> như trước — không để app chết vì không ghi
    /// được file cấu hình. Tính đúng 1 lần lúc nạp class.
    /// </summary>
    public static string RootDirectory { get; } = ResolveRootDirectory();

    /// <summary>True nếu đang dùng thư mục cạnh exe; false nếu đã phải rơi về %LocalAppData% (thư mục exe
    /// không ghi được). Dùng để log/hiển thị cho kỹ thuật viên biết cấu hình đang nằm ở đâu.</summary>
    public static bool UsingExeRelativeRoot { get; private set; }

    private static string ResolveRootDirectory()
    {
        var exeRelative = Path.Combine(AppContext.BaseDirectory, "AppData");
        if (CanWriteInto(exeRelative))
        {
            UsingExeRelativeRoot = true;
            return exeRelative;
        }

        UsingExeRelativeRoot = false;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EolTester");
    }

    private static bool CanWriteInto(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>File cấu hình JSON con người đọc/sửa tay (kết nối PLC, bản đồ I/O, profile spec, tham số test,
    /// ngôn ngữ, giám sát thanh ghi, cấu hình xuất CSV, tài khoản người dùng, trạng thái runtime).</summary>
    public static string ConfigDirectory => Path.Combine(RootDirectory, "Config");

    /// <summary>File dữ liệu + log: audit-log SQLite và log có cấu trúc (Serilog) theo ngày.</summary>
    public static string LogsDirectory => Path.Combine(RootDirectory, "Logs");

    public static string AuditLogDatabasePath => Path.Combine(LogsDirectory, "audit-log.db");
    public static string LogFilePath => Path.Combine(LogsDirectory, "eoltester-.log");

    /// <summary>Thư mục <c>SeedData\</c> cạnh exe — 4 file CSV cấu hình đi kèm bản build (chỉ-đọc theo quy ước,
    /// sửa bằng cách build lại). Thiếu file nào → hỏi khôi phục lúc khởi động (xem SeedFiles + App.xaml.cs).</summary>
    public static string SeedDataDirectory => Path.Combine(AppContext.BaseDirectory, "SeedData");

    /// <summary>
    /// Thư mục "DATA" nằm CẠNH file .exe đang chạy — nơi gợi ý mặc định để Import/Export CSV nhãn I/O ở tab
    /// Monitor. Cố ý khác <see cref="ConfigDirectory"/> để tránh nhầm lẫn.
    /// </summary>
    public static string IoLabelExportDirectory => Path.Combine(AppContext.BaseDirectory, "DATA");

    /// <summary>
    /// Thư mục "scanlog" cạnh file .exe — chứa <c>barcode-&lt;yyyyMMdd&gt;.log</c>, log chống trùng barcode
    /// riêng cho SCAN MODE (xem <see cref="BarcodeScanLogService"/>). Cố ý TÁCH khỏi file CSV kết quả (tùy
    /// chọn, cột do Admin tự cấu hình) — chống trùng là yêu cầu chất lượng bắt buộc, không thể phụ thuộc vào
    /// việc CSV export có được bật/cấu hình đúng hay không.
    /// </summary>
    public static string ScanLogDirectory => Path.Combine(AppContext.BaseDirectory, "scanlog");

    /// <summary>
    /// Thư mục lưu CSV kết quả MẶC ĐỊNH khi Admin chưa tự chọn (<c>CsvExportSettings.OutputDirectory</c> rỗng)
    /// — xuất CSV là tính năng bắt buộc của giai đoạn hiện tại (CLAUDE.md mục 4), không được phép "tắt ngầm"
    /// chỉ vì thiếu cấu hình. Ưu tiên 1: thư mục Documents của user hiện tại. Ưu tiên 2 (Documents không lấy
    /// được — VD chạy dưới tài khoản service không có profile, <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>
    /// trả về rỗng thay vì ném lỗi): thư mục <c>csv_export</c> cạnh file .exe.
    /// </summary>
    public static string DefaultCsvExportDirectory
    {
        get
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documents)) return Path.Combine(documents, "EolTester", "csv_export");
            return Path.Combine(AppContext.BaseDirectory, "csv_export");
        }
    }

    /// <summary>Thư mục CSV thật sự sẽ dùng: <paramref name="configured"/> nếu Admin đã tự chọn (Set Spec.),
    /// hoặc <see cref="DefaultCsvExportDirectory"/> nếu chưa. Dùng cả lúc ghi file
    /// (<see cref="CsvResultExportService"/>) lẫn lúc hiển thị lên UI Set Spec.
    /// (<see cref="ViewModels.SettingTabViewModel"/>) để 2 nơi luôn khớp nhau — Admin luôn thấy đúng đường dẫn
    /// đang thật sự được dùng, không bao giờ thấy ô trống trong khi CSV vẫn đang được ghi ở nơi khác.</summary>
    public static string ResolveCsvExportDirectory(string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? DefaultCsvExportDirectory : configured;
}
