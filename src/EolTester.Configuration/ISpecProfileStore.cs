using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public interface ISpecProfileStore
{
    Task<SpecProfile> LoadAsync(string model, CancellationToken ct = default);
    Task SaveAsync(SpecProfile profile, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken ct = default);
}
