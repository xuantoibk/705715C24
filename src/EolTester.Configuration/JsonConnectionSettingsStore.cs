using System.Text.Json;
using System.Text.Json.Serialization;
using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public sealed class JsonConnectionSettingsStore : IConnectionSettingsStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public JsonConnectionSettingsStore(string configRootDirectory)
    {
        Directory.CreateDirectory(configRootDirectory);
        _filePath = Path.Combine(configRootDirectory, "connection.json");
    }

    public async Task<ConnectionSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
        {
            var seeded = new ConnectionSettings();
            await SaveAsync(seeded, ct);
            return seeded;
        }

        await using var stream = File.OpenRead(_filePath);
        var settings = await JsonSerializer.DeserializeAsync<ConnectionSettings>(stream, JsonOptions, ct);
        return settings ?? new ConnectionSettings();
    }

    public async Task SaveAsync(ConnectionSettings settings, CancellationToken ct = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct);
    }
}
