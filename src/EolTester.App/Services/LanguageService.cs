using System.Globalization;
using EolTester.App.Localization;
using EolTester.Configuration;
using EolTester.Configuration.Models;

namespace EolTester.App.Services;

public sealed class LanguageService : ILanguageService
{
    private static readonly CultureInfo[] Cultures =
    [
        CultureInfo.GetCultureInfo("vi-VN"),
        CultureInfo.GetCultureInfo("en-US"),
    ];

    private readonly ILanguagePreferenceStore _store;

    public int CurrentLanguageIndex { get; private set; }
    public CultureInfo CurrentCulture => Cultures[CurrentLanguageIndex];
    public event EventHandler? LanguageChanged;

    public LanguageService(ILanguagePreferenceStore store)
    {
        _store = store;
    }

    public async Task InitializeAsync()
    {
        var preference = await _store.LoadAsync();
        CurrentLanguageIndex = preference.LanguageIndex is >= 0 and < 2 ? preference.LanguageIndex : 0;
        Translation.Instance.CurrentCulture = CurrentCulture;
    }

    public async Task SetLanguageAsync(int languageIndex)
    {
        if (languageIndex is < 0 or >= 2 || languageIndex == CurrentLanguageIndex) return;

        CurrentLanguageIndex = languageIndex;
        Translation.Instance.CurrentCulture = CurrentCulture;
        await _store.SaveAsync(new LanguagePreference { LanguageIndex = languageIndex });
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
