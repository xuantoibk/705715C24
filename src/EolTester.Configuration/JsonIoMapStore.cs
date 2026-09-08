using System.Text.Json;
using EolTester.Configuration.Models;
using EolTester.Core.Enums;
using EolTester.Core.Models;

namespace EolTester.Configuration;

public sealed class JsonIoMapStore : IIoMapStore
{
    private readonly string _filePath;
    private readonly IIoMapSeedSource _seedSource;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonIoMapStore(string configRootDirectory, IIoMapSeedSource seedSource)
    {
        Directory.CreateDirectory(configRootDirectory);
        _filePath = Path.Combine(configRootDirectory, "io-map.json");
        _seedSource = seedSource;
    }

    public async Task<IoMapProfile> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
        {
            var seeded = await CreateSeedProfileAsync(ct);
            await SaveAsync(seeded, ct);
            return seeded;
        }

        await using var stream = File.OpenRead(_filePath);
        var profile = await JsonSerializer.DeserializeAsync<IoMapProfile>(stream, JsonOptions, ct);
        return profile ?? await CreateSeedProfileAsync(ct);
    }

    public async Task SaveAsync(IoMapProfile profile, CancellationToken ct = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, ct);
    }

    /// <summary>
    /// Nạp dữ liệu khởi tạo từ SeedData/io-map-seed.csv (CHỈ dùng đúng 1 lần lúc io-map.json chưa tồn tại —
    /// không overlay lại mỗi lần load, khác spec-register-map.csv, vì io-map.json đã có Import/Export CSV
    /// runtime riêng ở tab Monitor, overlay sẽ xóa mất thay đổi người dùng đã Import). Fallback về danh sách
    /// hardcode cũ nếu file CSV không có mặt (phòng vệ cho môi trường không copy SeedData, VD unit test).
    /// </summary>
    private async Task<IoMapProfile> CreateSeedProfileAsync(CancellationToken ct)
    {
        var points = await _seedSource.LoadAsync(ct);
        return new IoMapProfile { Points = points?.ToList() ?? CreateFallbackSeedProfile().Points };
    }

    private static IoMapProfile CreateFallbackSeedProfile()
    {
        // PLC dùng địa chỉ bit dạng bát phân (octal) theo chuẩn Mitsubishi: X00-X07 rồi nhảy sang X10-X17
        // (không có X08/X09) — bitIndex 0-15 tuần tự cho đúng thứ tự hiển thị X00..X07,X10..X17.
        var points = new List<IoPointDefinition>();
        int order = 0;

        void AddInput(int bitIndex, string label)
        {
            var suffix = Convert.ToString(bitIndex, 8).PadLeft(2, '0');
            points.Add(new IoPointDefinition { Key = $"X{suffix}", Address = $"1{bitIndex:D4}", Direction = IoDirection.Input, Label1 = label, Order = order++ });
        }

        void AddOutput(int bitIndex, string label)
        {
            var suffix = Convert.ToString(bitIndex, 8).PadLeft(2, '0');
            points.Add(new IoPointDefinition { Key = $"Y{suffix}", Address = $"0{bitIndex:D4}", Direction = IoDirection.Output, Label1 = label, Order = order++ });
        }

        for (int i = 0; i <= 15; i++) AddInput(i, $"Ngõ vào X{Convert.ToString(i, 8).PadLeft(2, '0')}");
        for (int i = 0; i <= 15; i++) AddOutput(i, $"Ngõ ra Y{Convert.ToString(i, 8).PadLeft(2, '0')}");

        return new IoMapProfile { Points = points };
    }
}
