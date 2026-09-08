namespace EolTester.Configuration.Models;

/// <summary>Cấu hình tính năng xuất CSV kết quả (tab Set Spec., yêu cầu Admin) — lưu tại csv-export-settings.json.</summary>
public sealed class CsvExportSettings
{
    /// <summary>Thư mục lưu file CSV — rỗng nghĩa là Admin chưa tự chọn, KHÔNG có nghĩa "tắt tính năng"; xuất
    /// CSV là tính năng bắt buộc, khi rỗng dùng thư mục mặc định (xem EolTester.App.Services.AppPaths.
    /// ResolveCsvExportDirectory/DefaultCsvExportDirectory).</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>Danh sách "cột dữ liệu tùy chỉnh" — mỗi phần tử là 1 chuỗi thô Admin gõ tay (có gợi ý từ
    /// spec-register-map.csv + 4 token đặc biệt trên UI), parse lại mỗi lần dùng qua
    /// <see cref="EolTester.Core.CsvColumnSpecParser"/> (không cache dạng đã parse trong JSON) — hoặc token
    /// đặc biệt (&lt;STT&gt;/&lt;barcode&gt;/&lt;JOB&gt;/&lt;date&gt;/&lt;time&gt;/composite ngày-giờ) hoặc
    /// địa chỉ thanh ghi ("Dxxxx"/"Dxxxx.b"). Đổi tên từ <c>ColumnAddresses</c> (chỉ còn hợp lý khi mọi cột
    /// đều là địa chỉ thanh ghi) — không giữ shim tương thích ngược, JSON cũ mất field này coi như rỗng.</summary>
    public List<string> ColumnSpecs { get; set; } = [];
}
