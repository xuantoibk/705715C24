using System.Globalization;

namespace EolTester.App.Services;

/// <summary>
/// Chỉ số ngôn ngữ hiện tại (0 = vi-VN, 1 = en-US, 2 = dự phòng) — dùng chung cho cả chữ tĩnh trên UI
/// (qua <see cref="EolTester.App.Localization.Translation"/>) lẫn việc chọn slot nhãn Label1/2/3 của
/// từng điểm I/O ở tab Monitor.
/// </summary>
public interface ILanguageService
{
    int CurrentLanguageIndex { get; }
    CultureInfo CurrentCulture { get; }
    event EventHandler? LanguageChanged;

    Task InitializeAsync();
    Task SetLanguageAsync(int languageIndex);
}
