using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public interface ICsvExportSettingsStore
{
    Task<CsvExportSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CsvExportSettings settings, CancellationToken ct = default);
}
