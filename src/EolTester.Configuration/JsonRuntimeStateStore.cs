using System.Text.Json;
using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public interface IRuntimeStateStore
{
    Task<RuntimeState> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(RuntimeState state, CancellationToken ct = default);
}

/// <summary>Lưu/nạp <see cref="RuntimeState"/> ở <c>Config\runtime-state.json</c> — cùng pattern
/// <see cref="JsonTestParametersStore"/>. Đọc lỗi/thiếu file trả về <see cref="RuntimeState"/> rỗng (không ném).</summary>
public sealed class JsonRuntimeStateStore : IRuntimeStateStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonRuntimeStateStore(string configRootDirectory)
    {
        Directory.CreateDirectory(configRootDirectory);
        _filePath = Path.Combine(configRootDirectory, "runtime-state.json");
    }

    public async Task<RuntimeState> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath)) return new RuntimeState();
        try
        {
            await using var stream = File.OpenRead(_filePath);
            var state = await JsonSerializer.DeserializeAsync<RuntimeState>(stream, JsonOptions, ct);
            return state ?? new RuntimeState();
        }
        catch
        {
            return new RuntimeState();
        }
    }

    public async Task SaveAsync(RuntimeState state, CancellationToken ct = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, ct);
    }
}
