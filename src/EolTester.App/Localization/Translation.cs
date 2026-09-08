using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace EolTester.App.Localization;

/// <summary>
/// Singleton tra cứu chuỗi đa ngôn ngữ từ Resources/Strings.resx (mặc định/invariant = tiếng Việt)
/// và Strings.en-US.resx. Luôn truyền CultureInfo tường minh khi tra cứu — không dựa vào
/// Thread.CurrentUICulture — để đổi ngôn ngữ có hiệu lực ngay lập tức không cần khởi động lại.
/// Đổi <see cref="CurrentCulture"/> sẽ raise PropertyChanged("Item[]") theo đúng quy ước WPF cho
/// indexer binding, khiến mọi binding dạng Path=[Key] (kể cả qua TrExtension) tự refresh.
/// </summary>
public sealed class Translation : INotifyPropertyChanged
{
    public static Translation Instance { get; } = new();

    private readonly ResourceManager _resourceManager =
        new("EolTester.App.Resources.Strings", typeof(Translation).Assembly);

    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Ghi đè giá trị resx theo (culture, key) — dùng cho chuỗi cấu hình được ở ngoài resx (VD tiêu
    /// đề ứng dụng đọc từ spec-Default.csv, xem App.xaml.cs). Key định dạng "{cultureName}:{resxKey}".</summary>
    private readonly Dictionary<string, string> _overrides = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (Equals(_currentCulture, value)) return;
            _currentCulture = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    /// <summary>Ghi đè 1 chuỗi resx cho đúng 1 culture cụ thể — không đụng tới các culture/key khác. Raise
    /// PropertyChanged("Item[]") để mọi binding {loc:Tr} tự refresh ngay cả khi culture hiện tại không đổi.</summary>
    public void SetOverride(string cultureName, string key, string value)
    {
        _overrides[$"{cultureName}:{key}"] = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string this[string key] =>
        _overrides.TryGetValue($"{_currentCulture.Name}:{key}", out var over)
            ? over
            : _resourceManager.GetString(key, _currentCulture) ?? key;
}
