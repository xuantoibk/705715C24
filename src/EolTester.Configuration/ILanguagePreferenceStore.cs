using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public interface ILanguagePreferenceStore
{
    Task<LanguagePreference> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(LanguagePreference preference, CancellationToken ct = default);
}
