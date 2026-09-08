namespace EolTester.Configuration;

/// <summary>
/// Đọc thông tin mặc định của dự án (tiêu đề header theo ngôn ngữ, tên Model mặc định) từ 1 file CSV cố
/// định trong source tree (src/EolTester.Configuration/SeedData/spec-Default.csv, copy ra cùng thư mục exe
/// khi build) — không có UI Import/Export, chỉ đọc, sửa bằng cách sửa trực tiếp file CSV rồi build/publish
/// lại (xem App.xaml.cs — nạp đúng 1 lần lúc khởi động, trước khi tạo MainWindow).
/// </summary>
public sealed class CsvDefaultProjectInfoSource : IDefaultProjectInfoSource
{
    private readonly string _filePath;

    public CsvDefaultProjectInfoSource(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "SeedData", "spec-Default.csv");
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken ct = default) =>
        await CsvKeyValueFileReader.ReadAsync(_filePath, ct);
}
