using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EolTester.App.Services;
using EolTester.App.ViewModels.Rows;
using EolTester.Configuration;
using EolTester.Core.Enums;

namespace EolTester.App.ViewModels;

public partial class MainTabViewModel : ObservableObject
{
    private readonly PlcPollingService _polling;
    private readonly ISpecProfileStore _specStore;

    public ObservableCollection<TestStepRowViewModel> HighModeSteps { get; } = [];
    public ObservableCollection<TestStepRowViewModel> LowModeSteps { get; } = [];

    /// <summary>Thứ tự hiển thị cố định của bảng "Kiểm tra khác" — không dựa vào Order lưu trong
    /// spec-profile.json (file cũ qua nhiều phiên bản thêm/xóa bước có thể lưu Order lệch). Key lạ (không có
    /// trong danh sách) xếp cuối, giữ nguyên Order tương đối.</summary>
    private static readonly string[] OtherCheckKeyOrder = ["High.led1", "Low.led2", "High.mmtDir"];

    /// <summary>Các view chỉ-đọc cho 3 bảng ở tab Main: 2 bảng High/Low mode chỉ hiện bước có giới hạn số
    /// (<see cref="TestStepRowViewModel.IsConfigured"/>), bảng "Kiểm tra khác" gom các bước kiểu OK/NG thuần
    /// (Kiểm tra led / MMT Quay / Chiều quay MMT). Vẫn giữ nguyên <see cref="HighModeSteps"/>/<see cref="LowModeSteps"/>
    /// làm nguồn lưu trữ đầy đủ (SettingTabViewModel.ConfirmChangesAsync ghi cả cụm này ra spec-profile.json) —
    /// nếu tách hẳn các bước OK/NG ra collection riêng thì lần "Xác nhận thay đổi" kế tiếp sẽ xóa mất chúng.
    /// Snapshot List (không phải view sống) vì 2 collection gốc không Add/Remove sau LoadActiveModelAsync.</summary>
    public List<TestStepRowViewModel> HighModeConfiguredSteps => HighModeSteps.Where(s => s.IsConfigured).ToList();
    public List<TestStepRowViewModel> LowModeConfiguredSteps => LowModeSteps.Where(s => s.IsConfigured).ToList();
    public List<TestStepRowViewModel> OtherCheckSteps =>
        HighModeSteps.Concat(LowModeSteps).Where(s => !s.IsConfigured)
            .OrderBy(s =>
            {
                var i = Array.IndexOf(OtherCheckKeyOrder, s.Definition.Key);
                return i >= 0 ? i : int.MaxValue;
            })
            .ThenBy(s => s.Definition.Order)
            .ToList();

    public SignalIndicatorState Led1 => _polling.Led1;
    public SignalIndicatorState Led2 => _polling.Led2;
    public SignalIndicatorState Led3 => _polling.Led3;
    public SignalIndicatorState ProductDetected => _polling.ProductDetected;

    /// <summary>Cùng đối tượng <see cref="ShellViewModel.NgDetected"/> (khóa SIGNAL_NG_DETECTED, D71) — dùng
    /// để hiển thị ảnh OK/NG ở khối "Kết quả" tab Main, tự đồng bộ với ô "PHÁT HIỆN HÀNG NG" ở footer vì cùng
    /// trỏ tới <see cref="PlcPollingService.NgDetected"/>.</summary>
    public RegisterValueState NgDetected => _polling.NgDetected;

    /// <summary>Tên Model đang active (đọc từ SeedData/spec-Default.csv lúc App.xaml.cs gọi
    /// LoadActiveModelAsync) — nguồn duy nhất cho tên Model dùng khi lưu/nạp spec-profile.json, tránh lệch
    /// với 1 hằng số riêng ở SettingTabViewModel.</summary>
    public string ActiveModel { get; private set; } = string.Empty;

    public MainTabViewModel(PlcPollingService polling, ISpecProfileStore specStore, ILanguageService language)
    {
        _polling = polling;
        _specStore = specStore;

        // Description của 5 bước thuộc khung cố định High/Low mode tra qua resx (TestStepRowViewModel.Description)
        // — converter không tự re-run khi đổi ngôn ngữ (xem CLAUDE.md mục 10), nên phải chủ động re-raise từng
        // dòng khi đổi cờ VI/EN.
        language.LanguageChanged += (_, _) =>
        {
            foreach (var step in HighModeSteps.Concat(LowModeSteps)) step.RaiseDescriptionChanged();
        };
    }

    public async Task LoadActiveModelAsync(string model)
    {
        ActiveModel = model;
        var profile = await _specStore.LoadAsync(model);

        HighModeSteps.Clear();
        LowModeSteps.Clear();

        foreach (var step in profile.Steps.OrderBy(s => s.Order))
        {
            var row = new TestStepRowViewModel(step);
            if (step.Mode == TestMode.High) HighModeSteps.Add(row);
            else LowModeSteps.Add(row);
        }

        OnPropertyChanged(nameof(HighModeConfiguredSteps));
        OnPropertyChanged(nameof(LowModeConfiguredSteps));
        OnPropertyChanged(nameof(OtherCheckSteps));

        _polling.SetActiveSteps(HighModeSteps.Concat(LowModeSteps));
    }
}
