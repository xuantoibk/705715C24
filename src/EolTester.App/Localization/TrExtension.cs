using System.Windows.Data;
using System.Windows.Markup;

namespace EolTester.App.Localization;

/// <summary>
/// Cú pháp XAML gọn cho binding chuỗi đa ngôn ngữ: Text="{loc:Tr Header_Title}".
/// Bên trong chỉ là 1 Binding tới indexer của <see cref="Translation.Instance"/> — khi đổi ngôn ngữ,
/// Translation raise PropertyChanged("Item[]") khiến mọi Tr đang dùng tự refresh ngay lập tức.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public string Key { get; set; }

    public TrExtension() { Key = string.Empty; }

    public TrExtension(string key) { Key = key; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Translation.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
