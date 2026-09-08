using System.Text.Json;
using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public sealed class JsonLanguagePreferenceStore : ILanguagePreferenceStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonLanguagePreferenceStore(string configRootDirectory)
    {
        Directory.CreateDirectory(configRootDirectory);
        _filePath = Path.Combine(configRootDirectory, "language.json");
    }

    public async Task<LanguagePreference> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
        {
            var seeded = new LanguagePreference();
            await SaveAsync(seeded, ct);
            return seeded;
        }

        await using var stream = File.OpenRead(_filePath);
        var preference = await JsonSerializer.DeserializeAsync<LanguagePreference>(stream, JsonOptions, ct);
        return preference ?? new LanguagePreference();
    }

    public async Task SaveAsync(LanguagePreference preference, CancellationToken ct = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, preference, JsonOptions, ct);
    }
}
