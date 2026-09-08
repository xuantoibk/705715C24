using System.Text.Json;
using System.Text.Json.Serialization;
using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public sealed class JsonRegisterWatchStore : IRegisterWatchStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public JsonRegisterWatchStore(string configRootDirectory)
    {
        Directory.CreateDirectory(configRootDirectory);
        _filePath = Path.Combine(configRootDirectory, "register-watch.json");
    }

    public async Task<RegisterWatchProfile> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
            return new RegisterWatchProfile();

        await using var stream = File.OpenRead(_filePath);
        var profile = await JsonSerializer.DeserializeAsync<RegisterWatchProfile>(stream, JsonOptions, ct);
        return profile ?? new RegisterWatchProfile();
    }

    public async Task SaveAsync(RegisterWatchProfile profile, CancellationToken ct = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, ct);
    }
}
