namespace EolTester.Core;

/// <summary>
/// 1 cột đã cấu hình trong "Cột dữ liệu tùy chỉnh" (CSV kết quả, xem CsvColumnSpecParser) — 1 trong các loại
/// cố định (STT/Barcode/JOB ID/Date-Time) hoặc 1 thanh ghi PLC ("Dxxxx"/"Dxxxx.b"). Đóng (sealed hierarchy,
/// constructor private) để buộc mọi nơi xử lý phải liệt kê đủ các loại con qua pattern matching.
/// </summary>
public abstract record CsvColumnSpec
{
    private CsvColumnSpec() { }

    /// <summary>Số thứ tự dòng trong đúng file CSV đang ghi (1, 2, 3... reset khi sang file/ngày mới).</summary>
    public sealed record Sequence : CsvColumnSpec;

    /// <summary>Mã Barcode — mặc định luôn được tự chèn đầu danh sách cột nếu Admin không tự gõ token này.</summary>
    public sealed record BarcodeColumn : CsvColumnSpec;

    /// <summary>Job ID hiện tại của phần mềm (ShellViewModel.ConfirmedJobId) tại thời điểm ghi dòng.</summary>
    public sealed record JobIdColumn : CsvColumnSpec;

    /// <summary><paramref name="IsTime"/>=false: cột ngày, true: cột giờ. <paramref name="CustomFormat"/>
    /// null = định dạng ngắn mặc định theo CultureInfo.CurrentCulture (phản ánh cấu hình Windows của máy);
    /// khác null = chuỗi định dạng .NET đã thay từ sub-token (VD "&lt;yyyy&gt;-&lt;mm&gt;-&lt;dd&gt;" -&gt;
    /// "yyyy-MM-dd").</summary>
    public sealed record DateTimeColumn(bool IsTime, string? CustomFormat) : CsvColumnSpec;

    /// <summary>Thanh ghi PLC "Dxxxx"/"Dxxxx.b" — <paramref name="RawText"/> giữ nguyên chuỗi người dùng gõ,
    /// chưa validate cú pháp (EolTester.Core không có ModbusWordAddress — validate ở tầng gọi, App/Configuration).</summary>
    public sealed record RegisterColumn(string RawText) : CsvColumnSpec;
}
