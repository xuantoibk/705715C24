namespace EolTester.App.Services;

public interface ICsvResultExportService
{
    /// <summary>Ghi thêm 1 dòng vào file CSV kết quả "đang hoạt động" của ngày hôm nay (tự tạo file mới nếu
    /// chưa có hoặc cấu hình cột đã đổi so với file cũ, tự viết dòng tiêu đề nếu là file mới) — không làm gì
    /// nếu chưa cấu hình thư mục lưu (xem CsvExportSettings). <paramref name="jobId"/> dùng cho token
    /// &lt;JOB&gt; (ShellViewModel.ConfirmedJobId tại thời điểm ghi).</summary>
    Task AppendRowAsync(string barcode, string jobId, CancellationToken ct = default);
}
