using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public interface IConnectionSettingsStore
{
    Task<ConnectionSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(ConnectionSettings settings, CancellationToken ct = default);
}
