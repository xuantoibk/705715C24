using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EolTester.App.Localization;
using EolTester.App.ViewModels.Rows;
using EolTester.Configuration;
using EolTester.Configuration.Models;
using EolTester.Data;
using EolTester.Security;

namespace EolTester.App.ViewModels;

/// <summary>
/// Tab Set Spec. của máy 705/715 — CHỈ còn phần đặc thù máy này: bảng giới hạn High/Low mode (số bước đo, tên
/// phép đo gắn với tab Main của máy này) và nút "Xác nhận thay đổi". Phần dùng chung mọi máy (cấu hình quét
/// barcode, MODE TEST, Timer Setting, xuất CSV) đã tách sang <see cref="OperationalSettingsViewModel"/>/
/// <see cref="CsvExportSettingsViewModel"/> (Platform.Wpf, Singleton) — lớp này chỉ HAS-A (compose) 2 instance
/// đó để expose qua <see cref="ISettingTabViewModel"/>, không tự viết lại logic.
/// </summary>
public partial class SettingTabViewModel : ObservableObject, ISettingTabViewModel
{
    private readonly ISpecProfileStore _specStore;
    private readonly IAuditLogService _auditLog;
    private readonly IAuthenticationService _auth;
    private readonly MainTabViewModel _mainTab;
    private readonly Services.PlcPollingService _polling;

    public OperationalSettingsViewModel OperationalSettings { get; }
    public CsvExportSettingsViewModel CsvExportSettings { get; }

    public ObservableCollection<TestStepRowViewModel> HighModeSteps => _mainTab.HighModeSteps;
    public ObservableCollection<TestStepRowViewModel> LowModeSteps => _mainTab.LowModeSteps;

    /// <summary>Phải là <see cref="List{T}"/> (IList), KHÔNG được để nguyên IEnumerable lazy từ LINQ Where —
    /// xem docs/session-log/history.md (bẫy DataGrid BeginEdit).</summary>
    public List<TestStepRowViewModel> EditableHighModeSteps => HighModeSteps.Where(s => s.IsConfigured).ToList();
    public List<TestStepRowViewModel> EditableLowModeSteps => LowModeSteps.Where(s => s.IsConfigured).ToList();

    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private bool _canEditSpec;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusMessageIsError;

    private readonly Dictionary<string, (decimal? Lower, decimal? Upper)> _snapshotBeforeEdit = [];

    public SettingTabViewModel(
        ISpecProfileStore specStore,
        IAuditLogService auditLog,
        IAuthenticationService auth,
        MainTabViewModel mainTab,
        Services.PlcPollingService polling,
        OperationalSettingsViewModel operationalSettings,
        CsvExportSettingsViewModel csvExportSettings)
    {
        _specStore = specStore;
        _auditLog = auditLog;
        _auth = auth;
        _mainTab = mainTab;
        _polling = polling;
        OperationalSettings = operationalSettings;
        CsvExportSettings = csvExportSettings;
    }

    public void RefreshPermissions(bool canEditSpec, bool isAdmin)
    {
        CanEditSpec = canEditSpec;
        IsAdmin = isAdmin;
        OperationalSettings.RefreshPermissions(canEditSpec);
        CsvExportSettings.RefreshPermissions(isAdmin);
    }

    /// <summary>Nạp cả phần đặc thù máy (không có gì để nạp riêng ở đây — bảng bước đo đã nạp qua
    /// MainTabViewModel.LoadActiveModelAsync) lẫn phần dùng chung (Operational/CsvExport) — giữ 1 điểm gọi
    /// duy nhất từ App.xaml.cs, không đổi App.xaml.cs khi tách thành 2 sub-ViewModel.</summary>
    public async Task LoadParametersAsync()
    {
        await OperationalSettings.LoadAsync();
        await CsvExportSettings.LoadAsync();
    }

    public void SnapshotSteps()
    {
        _snapshotBeforeEdit.Clear();
        foreach (var step in HighModeSteps.Concat(LowModeSteps))
        {
            _snapshotBeforeEdit[step.Definition.Key] = (step.LowerLimit, step.UpperLimit);
        }
    }

    [RelayCommand]
    private async Task ConfirmChangesAsync()
    {
        if (!CanEditSpec) return;

        var outOfRangeSteps = HighModeSteps.Concat(LowModeSteps)
            .Where(s => s.IsLowerLimitOutOfRange || s.IsUpperLimitOutOfRange).ToList();
        if (outOfRangeSteps.Count > 0)
        {
            StatusMessageIsError = true;
            StatusMessage = string.Format(Translation.Instance["Setting_LimitOutOfRange"],
                string.Join(", ", outOfRangeSteps.Select(s => s.Description)));
            return;
        }

        var invalidSteps = HighModeSteps.Concat(LowModeSteps).Where(s => s.IsLimitRangeInvalid).ToList();
        if (invalidSteps.Count > 0)
        {
            StatusMessageIsError = true;
            StatusMessage = string.Format(Translation.Instance["Setting_InvalidLimitRange"],
                string.Join(", ", invalidSteps.Select(s => s.Description)));
            return;
        }
        StatusMessageIsError = false;

        var userName = _auth.CurrentUser?.UserName ?? "?";

        foreach (var step in HighModeSteps.Concat(LowModeSteps))
        {
            step.LowerLimit = step.EditLowerLimit;
            step.UpperLimit = step.EditUpperLimit;
            step.Definition.LowerLimit = step.LowerLimit;
            step.Definition.UpperLimit = step.UpperLimit;

            if (_snapshotBeforeEdit.TryGetValue(step.Definition.Key, out var before) &&
                (before.Lower != step.LowerLimit || before.Upper != step.UpperLimit))
            {
                await _auditLog.LogAsync(userName, $"Đổi giới hạn {step.Definition.Key}",
                    oldValue: $"[{before.Lower};{before.Upper}]",
                    newValue: $"[{step.LowerLimit};{step.UpperLimit}]");
            }
        }

        var allSteps = HighModeSteps.Concat(LowModeSteps).Select(s => s.Definition).ToList();
        if (allSteps.Count > 0)
        {
            await _specStore.SaveAsync(new SpecProfile { Model = _mainTab.ActiveModel, Steps = allSteps });
        }

        await _polling.WriteStepLimitsAsync(allSteps);
        await OperationalSettings.PersistAndPushParametersAsync();

        SnapshotSteps();
        StatusMessage = string.Format(Translation.Instance["Setting_SavedAt"], DateTime.Now.ToString("HH:mm:ss"));
    }
}
