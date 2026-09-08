using CommunityToolkit.Mvvm.ComponentModel;
using EolTester.App.Localization;

namespace EolTester.App.ViewModels.Rows;

/// <summary>1 dòng trong bảng "Timer Setting (ms)" ở tab Set Spec. — gồm 3 thời gian cố định (test High/Low
/// mode, giữ nút) xếp đầu bảng rồi tới 10 "Delay timer" tùy chỉnh. <see cref="Label"/> đọc qua resx key
/// (<see cref="LabelKey"/>), bị ghi đè theo culture từ spec-register-map.csv (cột Label1/Label2, khóa
/// <see cref="ParamKey"/>) lúc App.xaml.cs khởi động, nên tự đổi theo cờ ngôn ngữ VI/EN mà không cần rebuild
/// lại danh sách dòng (xem RaiseLabelChanged, gọi từ SettingTabViewModel khi ILanguageService.LanguageChanged).
/// ValueMs tự đẩy lên qua ValueCommitted khi đổi — SettingTabViewModel gộp vào PersistAndPushParametersAsync
/// (ghi xuống <see cref="ParamKey"/>, spec-register-map.csv), cùng cơ chế auto-save LostFocus như các tham số
/// cài đặt khác (ScanCodeLength...).</summary>
public sealed partial class TimerSettingRowViewModel(string labelKey, string paramKey) : ObservableObject
{
    /// <summary>Danh sách (LabelKey resx, ParamKey PLC) cho toàn bộ 13 dòng "Timer Setting" (3 thời gian cố
    /// định + 10 delay timer tùy chỉnh) — dùng chung giữa SettingTabViewModel.LoadParametersAsync (xây dựng
    /// danh sách dòng) và App.xaml.cs (ghi đè nhãn resx theo spec-register-map.csv), tránh lặp lại 2 nơi.</summary>
    public static readonly (string LabelKey, string ParamKey)[] Definitions =
        new (string LabelKey, string ParamKey)[]
        {
            ("Setting_HighDuration", "PARAM_HIGH_DURATION_MS"),
            ("Setting_LowDuration", "PARAM_LOW_DURATION_MS"),
            ("Setting_ButtonHold", "PARAM_BUTTON_HOLD_MS"),
        }
        .Concat(Enumerable.Range(1, 10).Select(i => ($"Setting_DelayTimer{i}", $"PARAM_DELAY_TIMER_{i}")))
        .ToArray();

    public string LabelKey { get; } = labelKey;
    public string ParamKey { get; } = paramKey;
    public string Label => Translation.Instance[LabelKey];

    [ObservableProperty] private int _valueMs;

    public event Action<TimerSettingRowViewModel>? ValueCommitted;

    partial void OnValueMsChanged(int value) => ValueCommitted?.Invoke(this);

    public void RaiseLabelChanged() => OnPropertyChanged(nameof(Label));
}
