namespace EolTester.App.Services;

public interface IBarcodeScanLogService
{
    /// <summary>SCAN MODE: barcode (fullcode) đã được ghi nhận (1 chu trình test đã hoàn tất, xem
    /// <see cref="RecordAsync"/>) trong ngày hôm nay chưa — dùng để chặn quét trùng lặp.</summary>
    Task<bool> IsRecordedTodayAsync(string barcode, CancellationToken ct = default);

    /// <summary>Ghi nhận 1 barcode đã hoàn tất chu trình test hôm nay — gọi cùng thời điểm với việc ghi dòng CSV
    /// kết quả (PLC báo <c>SIGNAL_CSV_WRITE</c>), nhưng độc lập hoàn toàn khỏi cấu hình CSV export.</summary>
    Task RecordAsync(string barcode, CancellationToken ct = default);
}
