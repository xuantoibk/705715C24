using System.Text.Json;
using System.Text.Json.Serialization;
using EolTester.Core.Models;

namespace EolTester.Configuration;

public sealed class JsonTestParametersStore : ITestParametersStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public JsonTestParametersStore(string configRootDirectory)
    {
        Directory.CreateDirectory(configRootDirectory);
        _filePath = Path.Combine(configRootDirectory, "test-parameters.json");
    }

    public async Task<TestParameters> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
        {
            var seeded = new TestParameters();
            await SaveAsync(seeded, ct);
            return seeded;
        }

        await using var stream = File.OpenRead(_filePath);
        var parameters = await JsonSerializer.DeserializeAsync<TestParameters>(stream, JsonOptions, ct);
        return parameters ?? new TestParameters();
    }

    public async Task SaveAsync(TestParameters parameters, CancellationToken ct = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, parameters, JsonOptions, ct);
    }
}
