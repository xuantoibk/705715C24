using CommunityToolkit.Mvvm.ComponentModel;
using EolTester.App.Services;
using EolTester.Core.Models;

namespace EolTester.App.ViewModels.Rows;

public partial class IoPointRowViewModel : ObservableObject
{
    private readonly ILanguageService _language;

    public IoPointRowViewModel(IoPointDefinition definition, ILanguageService language)
    {
        Definition = definition;
        _language = language;
        _language.LanguageChanged += (_, _) => OnPropertyChanged(nameof(DisplayLabel));
    }

    public IoPointDefinition Definition { get; }
    public string Key => Definition.Key;
    public string DisplayLabel => Definition.GetLabel(_language.CurrentLanguageIndex);

    /// <summary>Bit 1 (trạng thái, PLC ghi/PC đọc) — nguồn màu cho cột "Điểm".</summary>
    [ObservableProperty]
    private bool _value;

    /// <summary>
    /// Bit 2 (lệnh, PC ghi/PLC đọc) — độc lập với <see cref="Value"/>/Bit 1, dùng riêng để tô màu 2 nút
    /// ON/OFF ở khối "Điều khiển thủ công" (tab Monitor). Với điểm chưa nâng cấp <c>CommandAddress</c> riêng
    /// (chỉ có 1 bit), giá trị này luôn được giữ đồng bộ với <see cref="Value"/> — xem PlcPollingService.
    /// </summary>
    [ObservableProperty]
    private bool _commandValue;
}
