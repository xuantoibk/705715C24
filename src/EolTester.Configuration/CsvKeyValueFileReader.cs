using System.Linq;
using System.Text;

namespace EolTester.Configuration;

/// <summary>
/// Đọc 1 file CSV 2 cột "Key,Value" (UTF-8 có BOM) thành Dictionary — lõi dùng chung cho
/// <see cref="CsvSpecRegisterMapSource"/> (spec-register-map.csv) và <see cref="CsvDefaultProjectInfoSource"/>
/// (spec-Default.csv), cả 2 đều là "1 nguồn duy nhất, dễ tìm/sửa" checked-in trong SeedData, không có UI
/// Import/Export, chỉ đọc mỗi lần load.
/// </summary>
internal static class CsvKeyValueFileReader
{
    public static async Task<Dictionary<string, string>> ReadAsync(string filePath, CancellationToken ct)
    {
        var map = new Dictionary<string, string>();
        foreach (var row in await ReadRowsAsync(filePath, ct))
        {
            if (row.Length < 2) continue;

            var key = row[0];
            var value = row[1];
            if (key.Length == 0 || value.Length == 0) continue;

            map[key] = value;
        }

        return map;
    }

    /// <summary>Đọc toàn bộ dòng dữ liệu (bỏ dòng trống + dòng header đầu tiên) thành mảng cột đã trim —
    /// dùng cho các file CSV có nhiều hơn 2 cột (vd spec-register-map.csv có thêm cột "Scale") mà
    /// <see cref="ReadAsync"/> (chỉ lấy đúng cột 1-2) không đọc hết được.</summary>
    public static async Task<List<string[]>> ReadRowsAsync(string filePath, CancellationToken ct)
    {
        var rows = new List<string[]>();

        // File đĩa thiếu (chưa copy SeedData\, hoặc bị xóa và khôi phục ra đĩa thất bại) → đọc thẳng bản gốc
        // nhúng trong assembly để app vẫn chạy đúng (xem SeedFiles).
        Stream? source = File.Exists(filePath)
            ? File.OpenRead(filePath)
            : SeedFiles.OpenEmbedded(Path.GetFileName(filePath));
        if (source is null) return rows;

        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? line;
        bool skippedHeader = false;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!skippedHeader) { skippedHeader = true; continue; }

            rows.Add(line.Split(',').Select(p => p.Trim()).ToArray());
        }

        return rows;
    }
}
