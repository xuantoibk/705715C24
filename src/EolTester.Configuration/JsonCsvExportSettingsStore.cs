using System.Text.Json;
using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public sealed class JsonCsvExportSettingsStore : ICsvExportSettingsStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonCsvExportSettingsStore(string configRootDirectory)
    {
        Directory.CreateDirectory(configRootDirectory);
        _filePath = Path.Combine(configRootDirectory, "csv-export-settings.json");
    }

    public async Task<CsvExportSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
            return new CsvExportSettings();

        await using var stream = File.OpenRead(_filePath);
        var settings = await JsonSerializer.DeserializeAsync<CsvExportSettings>(stream, JsonOptions, ct);
        return settings ?? new CsvExportSettings();
    }

    public async Task SaveAsync(CsvExportSettings settings, CancellationToken ct = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct);
    }
}
