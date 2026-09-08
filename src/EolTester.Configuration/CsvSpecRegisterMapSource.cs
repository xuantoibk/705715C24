using EolTester.Core;

namespace EolTester.Configuration;

/// <summary>
/// Đọc ánh xạ Key -> Address (thanh ghi) + Key -> Scale (hệ số Gain) + Key -> Heading/Type (xuất CSV kết
/// quả) từ 1 file CSV nằm cố định trong source tree (src/EolTester.Configuration/SeedData/spec-register-map.csv,
/// copy ra cùng thư mục exe khi build) — đây là "1 nguồn" duy nhất, dễ tìm/sửa khi phát triển máy, thay vì
/// sửa rải rác trong code C#. File này KHÔNG có cơ chế Import/Export qua UI — chỉ đọc, coi như dữ liệu cấu
/// hình đi kèm bản build (theo đúng yêu cầu người dùng: "sẽ không thay đổi sau khi build code", sửa bằng
/// cách sửa trực tiếp file CSV rồi build lại).
/// Định dạng cột: Key,Heading,Type,Address,Scale,Label1,Label2. Heading/Type tùy chọn — chỉ có ý nghĩa với
/// các Key muốn xuất hiện trong "Cột dữ liệu tùy chỉnh" ở tab Set Spec. (xem CsvResultExportService); Scale
/// tùy chọn — chỉ có ý nghĩa với các Key đại diện 1 đại lượng đo lường, VD "High.voltage"; Label1/Label2 tùy
/// chọn — hiện chỉ dùng cho PARAM_DELAY_TIMER_1..10; các cột không dùng tới thì để trống.
/// <b>Lưu ý</b>: KHÔNG dùng <see cref="CsvKeyValueFileReader.ReadAsync"/> (giả định cột 2 = "value") cho Key
/// -> Address ở đây nữa vì cột 2 giờ là Heading, không phải Address — phải tự đọc qua
/// <see cref="CsvKeyValueFileReader.ReadRowsAsync"/> và lấy đúng chỉ số cột.
/// </summary>
public sealed class CsvSpecRegisterMapSource : ISpecRegisterMapSource
{
    private const int KeyIndex = 0;
    private const int HeadingIndex = 1;
    private const int TypeIndex = 2;
    private const int AddressIndex = 3;
    private const int ScaleIndex = 4;
    private const int Label1Index = 5;
    private const int Label2Index = 6;

    private readonly string _filePath;

    public CsvSpecRegisterMapSource(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "SeedData", "spec-register-map.csv");
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>();
        foreach (var row in await CsvKeyValueFileReader.ReadRowsAsync(_filePath, ct))
        {
            if (row.Length <= AddressIndex || row[KeyIndex].Length == 0 || row[AddressIndex].Length == 0) continue;
            map[row[KeyIndex]] = row[AddressIndex];
        }

        return map;
    }

    public async Task<IReadOnlyDictionary<string, int>> LoadScalesAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, int>();
        foreach (var row in await CsvKeyValueFileReader.ReadRowsAsync(_filePath, ct))
        {
            if (row.Length == 0 || row[KeyIndex].Length == 0) continue;

            var scale = 1;
            if (row.Length > ScaleIndex && int.TryParse(row[ScaleIndex], out var parsed) && PlcGainScale.IsValid(parsed))
            {
                scale = parsed;
            }

            map[row[KeyIndex]] = scale;
        }

        return map;
    }

    public async Task<IReadOnlyDictionary<string, (string? Label1, string? Label2)>> LoadLabelsAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, (string? Label1, string? Label2)>();
        foreach (var row in await CsvKeyValueFileReader.ReadRowsAsync(_filePath, ct))
        {
            if (row.Length <= Label1Index || row[KeyIndex].Length == 0) continue;

            var label1 = row[Label1Index].Length > 0 ? row[Label1Index] : null;
            var label2 = row.Length > Label2Index && row[Label2Index].Length > 0 ? row[Label2Index] : null;
            if (label1 is null && label2 is null) continue;

            map[row[KeyIndex]] = (label1, label2);
        }

        return map;
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadHeadingsAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>();
        foreach (var row in await CsvKeyValueFileReader.ReadRowsAsync(_filePath, ct))
        {
            if (row.Length <= HeadingIndex || row[KeyIndex].Length == 0 || row[HeadingIndex].Length == 0) continue;
            map[row[KeyIndex]] = row[HeadingIndex];
        }

        return map;
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadTypesAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>();
        foreach (var row in await CsvKeyValueFileReader.ReadRowsAsync(_filePath, ct))
        {
            if (row.Length <= TypeIndex || row[KeyIndex].Length == 0 || row[TypeIndex].Length == 0) continue;
            map[row[KeyIndex]] = row[TypeIndex];
        }

        return map;
    }
}
