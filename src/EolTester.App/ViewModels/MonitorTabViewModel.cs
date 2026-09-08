using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EolTester.App.Localization;
using EolTester.App.Services;
using EolTester.App.ViewModels.Rows;
using EolTester.Communication;
using EolTester.Configuration;
using EolTester.Configuration.Models;
using EolTester.Core.Enums;
using EolTester.Data;
using EolTester.Security;
using Microsoft.Win32;
using Serilog;

namespace EolTester.App.ViewModels;

public partial class MonitorTabViewModel : ObservableObject
{
    private const int ColumnSize = 16;

    private readonly PlcPollingService _polling;
    private readonly IAuditLogService _auditLog;
    private readonly IAuthenticationService _auth;
    private readonly IIoLabelCsvService _csvService;
    private readonly IIoMapStore _ioMapStore;

    public ObservableCollection<IoPointRowViewModel> Inputs => _polling.Inputs;
    public ObservableCollection<IoPointRowViewModel> Outputs => _polling.Outputs;

    public ObservableCollection<ObservableCollection<IoPointRowViewModel>> InputColumns { get; } = [];
    public ObservableCollection<ObservableCollection<IoPointRowViewModel>> OutputColumns { get; } = [];

    [ObservableProperty] private OperatingMode _mode = OperatingMode.Auto;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportLabelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportLabelCommand))]
    private bool _isAdmin;

    /// <summary>Đã đăng nhập (bất kỳ vai trò nào — User trở lên). Khách chưa đăng nhập chỉ được xem, không
    /// đổi Auto/Manual và không điều khiển I/O thủ công (xem CLAUDE.md mục 8).</summary>
    private bool _canOperate;

    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Phân biệt thông báo lỗi (đỏ) với thông báo thành công (xanh) ở dải trên tab Monitor — cùng
    /// pattern StatusMessage/StatusMessageIsError đã dùng ở SetupTabViewModel/SettingTabViewModel. Trước đây
    /// TextBlock luôn tô DarkRed nên "Import thành công" cũng hiện đỏ như lỗi.</summary>
    [ObservableProperty] private bool _statusMessageIsError;

    [ObservableProperty] private ConnectionState _connectionState = ConnectionState.Disconnected;

    /// <summary>Cùng lý do với ShellViewModel.CanSendCommands: yêu cầu đã đăng nhập (Khách chỉ được xem, xem
    /// CLAUDE.md mục 8) VÀ driver không báo lỗi rõ ràng (Error). Không khóa ở Disconnected (bình thường với
    /// Role=Slave hoặc Master chưa kịp connect) hay Connecting.</summary>
    public bool CanSendCommands => _canOperate && ConnectionState != ConnectionState.Error;

    public bool CanUseManualControls => Mode == OperatingMode.Manual && CanSendCommands;

    public MonitorTabViewModel(
        PlcPollingService polling,
        IAuditLogService auditLog,
        IAuthenticationService auth,
        IIoLabelCsvService csvService,
        IIoMapStore ioMapStore)
    {
        _polling = polling;
        _auditLog = auditLog;
        _auth = auth;
        _csvService = csvService;
        _ioMapStore = ioMapStore;

        Inputs.CollectionChanged += (_, _) => RebuildColumns();
        Outputs.CollectionChanged += (_, _) => RebuildColumns();
        RebuildColumns();

        ConnectionState = polling.State;
        polling.StateChanged += (_, state) => ConnectionState = state;
    }

    /// <summary>Gọi từ App.xaml.cs khi Role=Slave — cùng lý do ShellViewModel.ObserveSlaveConnectionState:
    /// polling.State luôn Disconnected ở Slave nên CanSendCommands/CanUseManualControls không tự khóa được
    /// khi service Slave thật báo Error.</summary>
    public void ObserveSlaveConnectionState(IPlcSlaveService slaveService)
    {
        ConnectionState = slaveService.State;
        slaveService.StateChanged += (_, state) => ConnectionState = state;
    }

    public void RefreshPermissions(bool canOperate, bool isAdmin)
    {
        IsAdmin = isAdmin;
        _canOperate = canOperate;
        OnPropertyChanged(nameof(CanSendCommands));
        OnPropertyChanged(nameof(CanUseManualControls));
    }

    partial void OnModeChanged(OperatingMode value) => OnPropertyChanged(nameof(CanUseManualControls));

    partial void OnConnectionStateChanged(ConnectionState value)
    {
        OnPropertyChanged(nameof(CanSendCommands));
        OnPropertyChanged(nameof(CanUseManualControls));
    }

    private void RebuildColumns()
    {
        RebuildColumnGroup(Inputs, InputColumns);
        RebuildColumnGroup(Outputs, OutputColumns);
    }

    private static void RebuildColumnGroup(
        ObservableCollection<IoPointRowViewModel> source,
        ObservableCollection<ObservableCollection<IoPointRowViewModel>> target)
    {
        target.Clear();
        foreach (var chunk in source.Chunk(ColumnSize))
        {
            target.Add(new ObservableCollection<IoPointRowViewModel>(chunk));
        }
    }

    [RelayCommand]
    private async Task SetModeAsync(string? modeName)
    {
        var newMode = modeName == "Manual" ? OperatingMode.Manual : OperatingMode.Auto;
        if (newMode == Mode) return;

        if (newMode == OperatingMode.Manual)
        {
            // "Bumpless transfer": mồi Bit 2 (lệnh) bằng đúng giá trị Bit 3 (PLC đang tự quản lý) trước khi
            // trao quyền điều khiển cho người vận hành — chỉ 1 lần đúng lúc chuyển đổi, không lặp lại sau đó.
            await _polling.TransferHandoverToCommandAsync(Outputs);
        }

        Mode = newMode;
        await _polling.SetLevelCommandAsync("CMD_MODE", newMode == OperatingMode.Manual);
        var userName = _auth.CurrentUser?.UserName ?? "?";
        await _auditLog.LogAsync(userName, "Đổi chế độ vận hành", newValue: Mode.ToString());
    }

    [RelayCommand]
    private async Task TurnOnAsync(IoPointRowViewModel? point)
    {
        if (point is null || !CanUseManualControls) return;
        await _polling.WriteOutputAsync(point, true);
        var userName = _auth.CurrentUser?.UserName ?? "?";
        await _auditLog.LogAsync(userName, $"Điều khiển thủ công {point.Key}", newValue: "True");
    }

    [RelayCommand]
    private async Task TurnOffAsync(IoPointRowViewModel? point)
    {
        if (point is null || !CanUseManualControls) return;
        await _polling.WriteOutputAsync(point, false);
        var userName = _auth.CurrentUser?.UserName ?? "?";
        await _auditLog.LogAsync(userName, $"Điều khiển thủ công {point.Key}", newValue: "False");
    }

    [RelayCommand(CanExecute = nameof(IsAdmin))]
    private async Task ImportLabelAsync()
    {
        if (!IsAdmin) return;
        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = Translation.Instance["Monitor_CsvFilter"],
                InitialDirectory = Directory.Exists(AppPaths.IoLabelExportDirectory) ? AppPaths.IoLabelExportDirectory : AppContext.BaseDirectory,
            };
            if (dialog.ShowDialog() != true) return;

            var result = await _csvService.ImportAsync(dialog.FileName);
            if (result.HasFatalErrors)
            {
                var lines = result.Errors.Select(e => string.Format(Translation.Instance["Monitor_CsvErrorRow"], e.RowNumber, DescribeIssue(e)));
                StatusMessageIsError = true;
                StatusMessage = string.Format(Translation.Instance["Monitor_ImportBlocked"], result.Errors.Count, string.Join("\n", lines));
                return;
            }

            await _ioMapStore.SaveAsync(new IoMapProfile { Points = result.Points.ToList() });
            await _polling.ReloadIoMapAsync();

            var userName = _auth.CurrentUser?.UserName ?? "?";
            await _auditLog.LogAsync(userName, "Nhập nhãn I/O từ CSV", newValue: $"{result.Points.Count} điểm");

            StatusMessageIsError = false;
            StatusMessage = string.Format(Translation.Instance["Monitor_ImportSuccess"], result.Points.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lỗi khi Import/Export nhãn I/O CSV");
            StatusMessageIsError = true;
            StatusMessage = string.Format(Translation.Instance["Monitor_UnexpectedError"], ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(IsAdmin))]
    private async Task ExportLabelAsync()
    {
        if (!IsAdmin) return;
        try
        {
            Directory.CreateDirectory(AppPaths.IoLabelExportDirectory);
            var dialog = new SaveFileDialog
            {
                Filter = Translation.Instance["Monitor_CsvFilter"],
                InitialDirectory = AppPaths.IoLabelExportDirectory,
                FileName = $"io-labels-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            };
            if (dialog.ShowDialog() != true) return;

            var allPoints = Inputs.Concat(Outputs).Select(r => r.Definition).ToList();
            await _csvService.ExportAsync(allPoints, dialog.FileName);

            var userName = _auth.CurrentUser?.UserName ?? "?";
            await _auditLog.LogAsync(userName, "Xuất nhãn I/O ra CSV", newValue: $"{allPoints.Count} điểm -> {dialog.FileName}");

            StatusMessageIsError = false;
            StatusMessage = string.Format(Translation.Instance["Monitor_ExportSuccess"], allPoints.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lỗi khi Import/Export nhãn I/O CSV");
            StatusMessageIsError = true;
            StatusMessage = string.Format(Translation.Instance["Monitor_UnexpectedError"], ex.Message);
        }
    }

    private static string DescribeIssue(CsvRowIssue issue) => issue.Kind switch
    {
        CsvRowIssueKind.MissingAddress => Translation.Instance["Monitor_CsvMissingAddress"],
        CsvRowIssueKind.MissingKey => Translation.Instance["Monitor_CsvMissingKey"],
        CsvRowIssueKind.MissingLabel1 => Translation.Instance["Monitor_CsvMissingLabel1"],
        CsvRowIssueKind.UnknownDirection => string.Format(Translation.Instance["Monitor_CsvUnknownDirection"], issue.Detail),
        CsvRowIssueKind.MissingLabel2 => Translation.Instance["Monitor_CsvMissingLabel2"],
        CsvRowIssueKind.MissingLabel3 => Translation.Instance["Monitor_CsvMissingLabel3"],
        CsvRowIssueKind.CommandOrHandoverOnInput => string.Format(Translation.Instance["Monitor_CsvCommandOrHandoverOnInput"], issue.Detail),
        _ => issue.Kind.ToString(),
    };
}
