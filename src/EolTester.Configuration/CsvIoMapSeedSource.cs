using System.Text;
using EolTester.Core.Enums;
using EolTester.Core.Models;

namespace EolTester.Configuration;

/// <summary>
/// Đọc bản đồ I/O mặc định từ 1 file CSV nằm cố định trong source tree
/// (src/EolTester.Configuration/SeedData/io-map-seed.csv, copy ra cùng thư mục exe khi build) — cùng
/// schema 7 cột với CsvIoLabelService (Address,Key,Label1,Label2,Label3,CommandAddress,HandoverAddress) để
/// nhất quán. File này KHÔNG có cơ chế Import/Export qua UI, chỉ đọc — CHỈ dùng làm dữ liệu khởi tạo lần
/// đầu khi io-map.json chưa tồn tại (khác spec-register-map.csv vốn ghi đè lại mỗi lần load: io-map.json
/// đã có cơ chế Import/Export CSV runtime riêng ở tab Monitor, overlay mỗi lần load sẽ xóa mất thay đổi
/// người dùng đã Import qua UI đó).
/// </summary>
public sealed class CsvIoMapSeedSource : IIoMapSeedSource
{
    private readonly string _filePath;

    public CsvIoMapSeedSource(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "SeedData", "io-map-seed.csv");
    }

    public async Task<IReadOnlyList<IoPointDefinition>?> LoadAsync(CancellationToken ct = default)
    {
        // File đĩa thiếu → thử bản gốc nhúng (xem SeedFiles); vẫn không có → null (JsonIoMapStore tự dùng
        // fallback 16+16 hardcode).
        Stream? source = File.Exists(_filePath)
            ? File.OpenRead(_filePath)
            : SeedFiles.OpenEmbedded(Path.GetFileName(_filePath));
        if (source is null) return null;

        var points = new List<IoPointDefinition>();
        int order = 0;

        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? line;
        bool skippedHeader = false;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!skippedHeader) { skippedHeader = true; continue; }

            var fields = line.Split(',');
            string address = fields.Length > 0 ? fields[0].Trim() : string.Empty;
            string key = fields.Length > 1 ? fields[1].Trim() : string.Empty;
            string label1 = fields.Length > 2 ? fields[2].Trim() : string.Empty;
            string? label2 = fields.Length > 3 && !string.IsNullOrWhiteSpace(fields[3]) ? fields[3].Trim() : null;
            string? label3 = fields.Length > 4 && !string.IsNullOrWhiteSpace(fields[4]) ? fields[4].Trim() : null;
            string? commandAddress = fields.Length > 5 && !string.IsNullOrWhiteSpace(fields[5]) ? fields[5].Trim() : null;
            string? handoverAddress = fields.Length > 6 && !string.IsNullOrWhiteSpace(fields[6]) ? fields[6].Trim() : null;

            if (address.Length == 0 || key.Length == 0 || label1.Length == 0) continue;

            IoDirection direction;
            if (key.StartsWith("X", StringComparison.OrdinalIgnoreCase)) direction = IoDirection.Input;
            else if (key.StartsWith("Y", StringComparison.OrdinalIgnoreCase)) direction = IoDirection.Output;
            else continue;

            points.Add(new IoPointDefinition
            {
                Key = key,
                Address = address,
                Direction = direction,
                Label1 = label1,
                Label2 = label2,
                Label3 = label3,
                CommandAddress = commandAddress,
                HandoverAddress = handoverAddress,
                Order = order++,
            });
        }

        return points.Count > 0 ? points : null;
    }
}
