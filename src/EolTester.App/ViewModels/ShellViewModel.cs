using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EolTester.App.Localization;
using EolTester.App.Services;
using EolTester.App.ViewModels.Rows;
using EolTester.Communication;
using EolTester.Configuration;
using EolTester.Core;
using EolTester.Core.Enums;
using EolTester.Core.Models;
using EolTester.Data;
using EolTester.Security;
using Serilog;

namespace EolTester.App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly IAuthenticationService _auth;
    private readonly IAuditLogService _auditLog;
    private readonly PlcPollingService _polling;
    private readonly ILanguageService _language;
    private readonly ICsvResultExportService _csvExport;
    private readonly IBarcodeScanLogService _scanLog;
    private readonly IRuntimeStateStore _runtimeStateStore;

    /// <summary>Debounce lưu runtime-state.json — hủy timer cũ mỗi lần giá trị đổi, chỉ ghi file sau ~1s
    /// "im lặng". Tắt không đúng quy trình trong 1s cuối cùng chấp nhận mất, đủ an toàn (đã thống nhất).</summary>
    private CancellationTokenSource? _runtimeSaveCts;

    /// <summary>SCAN MODE: số thứ tự serial của lần quét hợp lệ gần nhất (để so "phải bằng +1"), reset về null
    /// mỗi khi chuyển VÀO SCAN MODE (xem constructor, subscribe SettingTab.PropertyChanged) — theo đúng yêu cầu
    /// "lần đầu tiên bật lại Scan mode thì mặc định pass", không phụ thuộc lịch sử trước đó (kể cả từ REV MODE).</summary>
    private int? _previousSerialNumber;

    public MainTabViewModel MainTab { get; }
    public MonitorTabViewModel MonitorTab { get; }
    public SetupTabViewModel SetupTab { get; }
    public SettingTabViewModel SettingTab { get; }

    public ObservableCollection<string> NotificationHistory { get; } = [];

    [ObservableProperty] private int _selectedTabIndex;

    [ObservableProperty] private string _loginUserName = string.Empty;
    [ObservableProperty] private string _loginPassword = string.Empty;
    [ObservableProperty] private UserAccount? _currentUser;

    [ObservableProperty] private ConnectionState _connectionState = ConnectionState.Disconnected;

    [ObservableProperty] private string _model = "705/715";
    [ObservableProperty] private DateTime _lotDate = DateTime.Today;
    [ObservableProperty] private string _jobName = "JOB01";
    [ObservableProperty] private string _confirmedJobId = string.Empty;

    [ObservableProperty] private TimeSpan _cycleTime;
    [ObservableProperty] private bool _isRunning;

    [ObservableProperty] private string _scanBarcodeText = string.Empty;
    [ObservableProperty] private string _lastScanOk = string.Empty;
    [ObservableProperty] private string _nextSerial = string.Empty;

    /// <summary>Giá trị thanh ghi "Phát hiện Hàng NG" (khóa SIGNAL_NG_DETECTED, spec-register-map.csv) — quy
    /// ước 1=xanh, 2=cam, giá trị khác/null=xám. Pass-through từ PlcPollingService (cùng đối tượng, cùng
    /// pattern MainTabViewModel.Led1/Led2/ProductDetected).</summary>
    public RegisterValueState NgDetected => _polling.NgDetected;

    /// <summary>Nhãn chữ động cho ô "PHÁT HIỆN HÀNG NG" ở footer — theo đúng giá trị SIGNAL_NG_DETECTED
    /// (D71): 0/null = chưa có kết quả, 1 = hàng OK, 2 = phát hiện NG. Property tính toán (không dùng converter
    /// — xem CLAUDE.md mục 10: converter không tự re-run khi đổi ngôn ngữ), re-raise khi NgDetected.Value đổi
    /// (subscribe trong constructor) hoặc khi đổi ngôn ngữ (_language.LanguageChanged).</summary>
    public string NgStatusText => NgDetected.Value switch
    {
        1 => Translation.Instance["Footer_NgStatusOk"],
        2 => Translation.Instance["Footer_NgStatusNg"],
        _ => Translation.Instance["Footer_NgStatusNone"],
    };

    public string StartButtonLabel => IsRunning ? "STOP" : "START";

    public string OperationStatusText
    {
        get
        {
            if (!IsRunning) return "WAITING START";
            return MonitorTab.Mode == OperatingMode.Manual ? "RUN MANUAL" : "RUN AUTO";
        }
    }

    public string ConnectionStateText => ConnectionState switch
    {
        ConnectionState.Connected => Translation.Instance["Common_Connected"],
        ConnectionState.Connecting => Translation.Instance["Common_Connecting"],
        ConnectionState.Error => Translation.Instance["Common_ConnectionError"],
        _ => Translation.Instance["Common_Disconnected"],
    };

    /// <summary>Cho phép nút Start/Stop/Reset/Xác nhận NG ở footer. Yêu cầu: (1) đã đăng nhập (bất kỳ vai trò
    /// nào — Khách chỉ được chuyển tab + quét barcode để vận hành, xem CLAUDE.md mục 8) VÀ (2) kết nối không
    /// báo lỗi rõ ràng. CỐ Ý không disable ở Disconnected/Connecting — Disconnected là trạng thái bình thường
    /// của Role=Slave, không phải lỗi.</summary>
    public bool CanSendCommands => IsLoggedIn && ConnectionState != ConnectionState.Error;

    public int CurrentLanguageIndex => _language.CurrentLanguageIndex;

    public bool IsLoggedIn => CurrentUser is not null;

    /// <summary>Quyền sửa thông số tab Set Spec. — từ Operator trở lên (User chỉ vận hành Main/Monitor).</summary>
    public bool CanEditSpec => CurrentUser?.Role >= UserRole.Operator;

    public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;

    public IReadOnlyList<string> AvailableUserNames => _auth.AvailableUserNames;

    public ShellViewModel(
        IAuthenticationService auth,
        IAuditLogService auditLog,
        PlcPollingService polling,
        ILanguageService language,
        ICsvResultExportService csvExport,
        IBarcodeScanLogService scanLog,
        IRuntimeStateStore runtimeStateStore,
        MainTabViewModel mainTab,
        MonitorTabViewModel monitorTab,
        SetupTabViewModel setupTab,
        SettingTabViewModel settingTab)
    {
        _auth = auth;
        _auditLog = auditLog;
        _polling = polling;
        _language = language;
        _csvExport = csvExport;
        _scanLog = scanLog;
        _runtimeStateStore = runtimeStateStore;
        MainTab = mainTab;
        MonitorTab = monitorTab;
        SetupTab = setupTab;
        SettingTab = settingTab;

        ConnectionState = polling.State;
        polling.StateChanged += (_, state) => ConnectionState = state;

        NgDetected.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RegisterValueState.Value)) OnPropertyChanged(nameof(NgStatusText));
        };

        // PLC báo đã xác nhận mã vừa quét (SIGNAL_SCAN_CONFIRM) — move ScanBarcodeText hiện tại sang
        // LastScanOk, tách biệt với đường xác nhận qua UI (Enter/prefix/độ dài, KeyboardWedgeScanCapture).
        // Fire-and-forget (CommitScanAsync giờ là async, tự bọc try/catch riêng — không để exception bay ra
        // ngoài event handler).
        polling.ScanConfirmSignalRaised += (_, _) => _ = CommitScanAsync();

        // PLC báo đã sẵn sàng ghi 1 dòng kết quả ra CSV (SIGNAL_CSV_WRITE) — ghi kèm LastScanOk hiện tại (mã
        // đã xác nhận gần nhất, KHÔNG phải ScanBarcodeText đang gõ dở). Bọc try/catch (không dùng async void
        // trực tiếp trên event) để 1 lỗi ghi file (VD ổ đĩa đầy, thư mục cấu hình sai) không crash app — cùng
        // nguyên tắc đã áp dụng cho mọi lệnh ghi PLC khác, xem CLAUDE.md mục 6. Cùng lúc ghi nhận barcode vào
        // scanlog chống trùng (IBarcodeScanLogService) — độc lập hoàn toàn với CSV export (có thể đang tắt/lỗi),
        // 2 tác vụ chạy song song, lỗi bên này không ảnh hưởng bên kia. CẢ HAI đều dùng LastScanOk ("Mã Scan
        // ghi nhận" — giá trị đã xác nhận qua CommitScanAsync), KHÔNG phải ScanBarcodeText ("Mã Scan quét
        // được" — buffer đang gõ dở, có thể chưa xác nhận/còn dở dang). Nếu LastScanOk đang rỗng (VD PLC bắn
        // SIGNAL_CSV_WRITE trước khi có bất kỳ lần quét hợp lệ nào trong chu trình — không đúng giả định bình
        // thường) thì bỏ qua cả 2, không ghi dòng CSV/scanlog rỗng.
        polling.CsvWriteSignalRaised += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(LastScanOk))
            {
                Log.Warning("SIGNAL_CSV_WRITE nhận được nhưng Mã Scan ghi nhận đang rỗng — bỏ qua ghi CSV/scanlog");
                AddNotification(Translation.Instance["Notify_CsvWriteSkippedEmptyBarcode"]);
                return;
            }

            _ = AppendCsvRowSafeAsync();
            _ = RecordScanLogSafeAsync();
        };

        // Đổi SCAN MODE ↔ REV MODE: điều chỉnh cách D104.2 (PARAM_SCAN_CODE_READY) được lái.
        //  - Vào REV MODE: bit bám theo MỨC "LastScanOk khác rỗng" (đẩy lại ngay theo giá trị hiện tại).
        //  - Vào SCAN MODE: bit chỉ còn theo xung CMD_START → ép về 0, chờ lần Start kế tiếp; đồng thời reset
        //    "số thứ tự lần quét hợp lệ gần nhất" (lần quét đầu sau khi chuyển luôn pass bước so thứ tự).
        SettingTab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SettingTabViewModel.ScanRevMode)) return;

            if (SettingTab.ScanRevMode)
            {
                _ = PushScanCodeReadyBitSafeAsync(!string.IsNullOrWhiteSpace(LastScanOk));
            }
            else
            {
                _ = PushScanCodeReadyBitSafeAsync(false);
                _previousSerialNumber = null;
                ScheduleRuntimeStateSave();
            }
        };

        // Bất kỳ lần ghi thanh ghi tham số/giới hạn nào → debounce lưu runtime-state.json (tính năng an toàn).
        _polling.PersistableStateChanged += (_, _) => ScheduleRuntimeStateSave();

        _auth.CurrentUserChanged += (_, _) =>
        {
            CurrentUser = _auth.CurrentUser;
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(CanEditSpec));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(CanSendCommands));
            MonitorTab.RefreshPermissions(IsLoggedIn, IsAdmin);
            SettingTab.RefreshPermissions(CanEditSpec, IsAdmin);
            SetupTab.RefreshPermissions(IsAdmin);
        };

        MonitorTab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MonitorTabViewModel.Mode)) OnPropertyChanged(nameof(OperationStatusText));
            if (e.PropertyName == nameof(MonitorTabViewModel.StatusMessage) && !string.IsNullOrWhiteSpace(MonitorTab.StatusMessage))
                AddNotification(MonitorTab.StatusMessage);
        };

        _language.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ConnectionStateText));
            OnPropertyChanged(nameof(CurrentLanguageIndex));
            OnPropertyChanged(nameof(NgStatusText));
        };
    }

    /// <summary>Gọi từ App.xaml.cs khi Role=Slave — polling.State luôn Disconnected trong trường hợp này
    /// (PlcPollingService không gán driver khi Slave), nên badge cần bind trực tiếp vào service Slave đang
    /// hoạt động (ModbusSlaveService/McProtocolSlaveService/SlmpSlaveService).</summary>
    public void ObserveSlaveConnectionState(IPlcSlaveService slaveService)
    {
        ConnectionState = slaveService.State;
        slaveService.StateChanged += (_, state) => ConnectionState = state;
    }

    [RelayCommand]
    private async Task SetLanguageAsync(string? languageIndex)
    {
        if (int.TryParse(languageIndex, out var index)) await _language.SetLanguageAsync(index);
    }

    partial void OnConnectionStateChanged(ConnectionState value)
    {
        OnPropertyChanged(nameof(ConnectionStateText));
        OnPropertyChanged(nameof(CanSendCommands));
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (_auth.Login(LoginUserName, LoginPassword))
        {
            LoginPassword = string.Empty;
            AddNotification(string.Format(Translation.Instance["Notify_Login"], LoginUserName));
            await _auditLog.LogAsync(LoginUserName, "Đăng nhập");
        }
        else
        {
            // Lỗi đăng nhập đưa vào lịch sử thông báo/lỗi thay vì hiện text ngay cạnh ô đăng nhập ở header —
            // text dài (VD "Sai tên đăng nhập hoặc mật khẩu") từng đẩy rộng cột 2, khiến TabControl (Width=*)
            // bị co lại và có thể làm tiêu đề tab wrap xuống 2 dòng.
            AddNotification(Translation.Instance["Login_Error"]);
            // Tự chuyển về tab Main (nơi có khối "Lịch sử thông báo/lỗi") để người dùng thấy thông báo ngay —
            // tránh trường hợp họ đang ở tab khác (Monitor/Setup/Setting) và không biết đăng nhập đã thất bại.
            SelectedTabIndex = 0;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var userName = CurrentUser?.UserName ?? "?";
        _auth.Logout();
        AddNotification(string.Format(Translation.Instance["Notify_Logout"], userName));
        await _auditLog.LogAsync(userName, "Đăng xuất");
    }

    [RelayCommand]
    private void ConfirmJob()
    {
        ConfirmedJobId = new JobInfo { Model = Model, Date = DateOnly.FromDateTime(LotDate), JobName = JobName }.JobId;
        AddNotification(string.Format(Translation.Instance["Notify_ConfirmJob"], ConfirmedJobId));
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (!IsRunning)
        {
            // SCAN MODE: D104.2 nâng cùng CMD_START khi lần Start thủ công này có "Mã Scan ghi nhận" hợp lệ,
            // luôn hạ khi xung tắt. REV MODE: truyền null — bit do OnLastScanOkChanged lái theo mức LastScanOk,
            // PulseCommandAsync không đụng tới (xem PlcPollingService.PulseCommandAsync).
            await _polling.PulseCommandAsync("CMD_START",
                scanCodeReadyForThisStart: SettingTab.ScanRevMode ? null : !string.IsNullOrWhiteSpace(LastScanOk));
            IsRunning = true;
            AddNotification(Translation.Instance["Notify_Start"]);
        }
        else
        {
            await _polling.PulseCommandAsync("CMD_STOP");
            IsRunning = false;
            AddNotification(Translation.Instance["Notify_Stop"]);
        }
    }

    [RelayCommand]
    private async Task ResetPressAsync()
    {
        await _polling.SetLevelCommandAsync("CMD_RESET", true);
        IsRunning = false;
        NgDetected.Value = null;
        CycleTime = TimeSpan.Zero;
        AddNotification(Translation.Instance["Notify_Reset"]);
    }

    [RelayCommand]
    private async Task ResetReleaseAsync()
    {
        await _polling.SetLevelCommandAsync("CMD_RESET", false);
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(StartButtonLabel));
        OnPropertyChanged(nameof(OperationStatusText));
    }

    [RelayCommand]
    private async Task ConfirmNgPressAsync()
    {
        await _polling.SetLevelCommandAsync("CMD_CONFIRM_NG", true);
        NgDetected.Value = null;
        AddNotification(Translation.Instance["Notify_ConfirmNg"]);
    }

    [RelayCommand]
    private async Task ConfirmNgReleaseAsync()
    {
        await _polling.SetLevelCommandAsync("CMD_CONFIRM_NG", false);
    }

    /// <summary>
    /// Chốt 1 lần quét/gõ tay xong — dùng chung cho mọi đường vào: <see cref="Behaviors.KeyboardWedgeScanCapture"/>
    /// (cả 3 chế độ PA1/PA2/PA3, xem CLAUDE.md mục "Đầu đọc barcode"), gõ tay trực tiếp + Enter (KeyBinding
    /// ở MainWindow.xaml), và <see cref="PlcPollingService.ScanConfirmSignalRaised"/>. TRƯỚC ĐÂY
    /// <c>OnScanBarcodeTextChanged</c> tự commit trên MỖI ký tự gõ (do UpdateSourceTrigger=PropertyChanged) —
    /// với chuỗi nhiều ký tự chỉ ký tự CUỐI CÙNG được giữ lại, bug thật đã gặp. Giờ ScanBarcodeText chỉ còn là
    /// buffer hiển thị sống, không có side-effect khi đổi giá trị — commit chỉ xảy ra đúng 1 lần khi gọi lệnh này.
    /// <para>
    /// REV MODE (<c>SettingTab.ScanRevMode=true</c>): commit thẳng như trước, không kiểm tra gì thêm. SCAN MODE
    /// (mặc định): chạy pipeline kiểm tra theo thứ tự —
    /// <list type="number">
    /// <item>Check 4 (LUÔN chạy, ĐẦU TIÊN): mã vừa quét phải khác <see cref="LastScanOk"/> ("Mã Scan ghi
    /// nhận" gần nhất). Trùng → ghi PARAM_SCAN_DUP_ERROR (D104.6) = 1, báo lỗi, dừng.</item>
    /// <item>Check 1 (LUÔN chạy): trích được số thứ tự serial từ đoạn con cấu hình (Set Spec.).</item>
    /// <item>Check 2 (chỉ khi <c>SettingTab.RequireScanOrder</c>): serial phải bằng lần quét hợp lệ trước + 1.</item>
    /// <item>Check 3 (chỉ khi <c>SettingTab.CheckDuplicateInLog</c>): mã chưa có trong scanlog chống trùng hôm nay.</item>
    /// </list>
    /// Chỉ khi TOÀN BỘ pipeline pass mới thực sự commit + ghi PARAM_SCAN_DUP_ERROR = 0 + tự gửi CMD_START (1
    /// giây) thay operator bấm Start. Fail bất kỳ bước nào: chỉ báo lỗi, KHÔNG xóa <see cref="ScanBarcodeText"/>
    /// (lần quét kế tiếp tự ghi đè), không commit, không gửi CMD_START; D104.6 chỉ về 0 khi pipeline pass hẳn.
    /// <see cref="SettingTabViewModel.RequireScanOrder"/>/<see cref="SettingTabViewModel.CheckDuplicateInLog"/>
    /// là 2 option độc lập — SCAN MODE không còn tự bật/khóa chúng.
    /// </para>
    /// </summary>
    [RelayCommand]
    private async Task CommitScanAsync()
    {
        try
        {
            var trimmed = ScanBarcodeText.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return;

            if (SettingTab.ScanRevMode)
            {
                LastScanOk = trimmed;
                ScanBarcodeText = string.Empty;
                AddNotification(string.Format(Translation.Instance["Notify_Scan"], LastScanOk));
                return;
            }

            // Check 4 — chạy đầu tiên: mã quét được phải khác "Mã Scan ghi nhận" gần nhất.
            if (string.Equals(trimmed, LastScanOk, StringComparison.Ordinal))
            {
                await _polling.WriteParamBitAsync("PARAM_SCAN_DUP_ERROR", true);
                AddNotification(string.Format(Translation.Instance["Notify_ScanRejectedSameAsLast"], trimmed));
                return;
            }

            // Check 1 — luôn trích số serial trong SCAN MODE (kể cả khi không kiểm tra thứ tự tem).
            if (!BarcodeSerialExtractor.TryExtractSerialNumber(trimmed, SettingTab.SerialStartIndex, SettingTab.SerialEndIndex, out var serialNumber))
            {
                AddNotification(string.Format(Translation.Instance["Notify_ScanRejectedInvalidSerial"], trimmed));
                return;
            }

            // Check 2 — chỉ khi bật "Bắt buộc đúng thứ tự tem".
            if (SettingTab.RequireScanOrder && _previousSerialNumber is int previous && serialNumber != previous + 1)
            {
                AddNotification(string.Format(Translation.Instance["Notify_ScanRejectedOrder"], trimmed, serialNumber, previous + 1));
                return;
            }

            // Check 3 — chỉ khi bật "Check Duplicate in log".
            if (SettingTab.CheckDuplicateInLog && await _scanLog.IsRecordedTodayAsync(trimmed))
            {
                AddNotification(string.Format(Translation.Instance["Notify_ScanRejectedDuplicate"], trimmed));
                return;
            }

            _previousSerialNumber = serialNumber;
            LastScanOk = trimmed;
            ScanBarcodeText = string.Empty;
            await _polling.WriteParamBitAsync("PARAM_SCAN_DUP_ERROR", false);
            AddNotification(string.Format(Translation.Instance["Notify_Scan"], LastScanOk));

            await _polling.PulseCommandAsync("CMD_START", pulseMs: 1000, scanCodeReadyForThisStart: true);
            IsRunning = true;
            AddNotification(Translation.Instance["Notify_Start"]);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lỗi xử lý mã quét (SCAN MODE)");
            AddNotification(string.Format(Translation.Instance["Notify_ScanPipelineError"], ScanBarcodeText, ex.Message));
        }
    }

    /// <summary>SCAN MODE (ScanRevMode=false): có cho phép ký tự từ đầu đọc barcode vào
    /// <see cref="ScanBarcodeText"/> lúc này không — máy phải đang chờ (SIGNAL_MACHINE_WAITING=0) và chưa tự
    /// gửi CMD_START (D100=0). Gọi từ <see cref="Behaviors.KeyboardWedgeScanCapture"/> mỗi ký tự gõ (đọc live,
    /// không cache). REV MODE (true) luôn cho qua — giữ nguyên hành vi cũ.</summary>
    public bool IsScanInputAllowedNow() => SettingTab.ScanRevMode || _polling.IsMachineReadyForScan();

    /// <summary>Gọi từ <see cref="PlcPollingService.CsvWriteSignalRaised"/> — ghi 1 dòng CSV kèm
    /// <see cref="LastScanOk"/> hiện tại. Nuốt exception + log, không để lỗi ghi file (đầy ổ đĩa, mất quyền
    /// ghi thư mục cấu hình...) bay ra ngoài làm crash app.</summary>
    private async Task AppendCsvRowSafeAsync()
    {
        try
        {
            await _csvExport.AppendRowAsync(LastScanOk, ConfirmedJobId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Không thể ghi dòng CSV kết quả");
            AddNotification(string.Format(Translation.Instance["Notify_CsvWriteFailed"], ex.Message));
        }
    }

    /// <summary>Ghi nhận <see cref="LastScanOk"/> vào scanlog chống trùng (xem <see cref="IBarcodeScanLogService"/>)
    /// — cùng thời điểm với <see cref="AppendCsvRowSafeAsync"/> nhưng độc lập hoàn toàn, không phụ thuộc CSV
    /// export có được cấu hình hay không. Nuốt exception + log, không để lỗi ghi file làm crash app.</summary>
    private async Task RecordScanLogSafeAsync()
    {
        try
        {
            await _scanLog.RecordAsync(LastScanOk);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Không thể ghi scanlog chống trùng barcode");
            AddNotification(string.Format(Translation.Instance["Notify_ScanLogWriteFailed"], ex.Message));
        }
    }

    /// <summary>PARAM_SCAN_CODE_READY (D104.2) được lái theo mode:
    /// <list type="bullet">
    /// <item>SCAN MODE: bám đúng vòng đời xung CMD_START (xem <see cref="PlcPollingService.PulseCommandAsync"/>)
    /// — KHÔNG đẩy ở đây.</item>
    /// <item>REV MODE: bám theo MỨC "Mã Scan ghi nhận khác rỗng" — đẩy lại mỗi khi <see cref="LastScanOk"/> đổi.</item>
    /// </list>
    /// Luôn debounce lưu runtime-state.json khi "Mã Scan ghi nhận" đổi.</summary>
    partial void OnLastScanOkChanged(string value)
    {
        if (SettingTab.ScanRevMode) _ = PushScanCodeReadyBitSafeAsync(!string.IsNullOrWhiteSpace(value));
        ScheduleRuntimeStateSave();
    }

    /// <summary>Nạp lại trạng thái runtime đã lưu (LastScanOk + số serial gần nhất + ảnh chụp thanh ghi
    /// D100-D199 không phải CMD_) — gọi 1 lần lúc khởi động, TRƯỚC khi các store chuyên biệt
    /// (test-parameters.json/spec-profile.json) nạp+đẩy, để store chuyên biệt vẫn thắng cho phần chúng quản lý.
    /// Nuốt lỗi (file hỏng/thiếu) — không chặn khởi động.</summary>
    public async Task RestoreRuntimeStateAsync()
    {
        try
        {
            var state = await _runtimeStateStore.LoadAsync();
            if (state.Registers.Count > 0) _polling.RestorePersistableD1xxRegisters(state.Registers);
            _previousSerialNumber = state.PreviousSerialNumber;
            if (!string.IsNullOrWhiteSpace(state.LastScanOk)) LastScanOk = state.LastScanOk;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Không thể nạp runtime-state.json — bỏ qua, khởi động bình thường");
        }
    }

    private void ScheduleRuntimeStateSave()
    {
        _runtimeSaveCts?.Cancel();
        var cts = new CancellationTokenSource();
        _runtimeSaveCts = cts;
        _ = SaveRuntimeStateAfterDelayAsync(cts.Token);
    }

    private async Task SaveRuntimeStateAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(1000, ct);
            var state = new EolTester.Configuration.Models.RuntimeState
            {
                LastScanOk = LastScanOk,
                PreviousSerialNumber = _previousSerialNumber,
                Registers = await _polling.SnapshotPersistableD1xxRegistersAsync(ct),
            };
            await _runtimeStateStore.SaveAsync(state, ct);
        }
        catch (OperationCanceledException) { /* bị hủy vì có thay đổi mới hơn — lần sau sẽ ghi */ }
        catch (Exception ex)
        {
            Log.Error(ex, "Không thể lưu runtime-state.json");
        }
    }

    /// <summary>Đẩy mức PARAM_SCAN_CODE_READY (D104.2) xuống PLC — CHỈ dùng cho REV MODE (bám theo "Mã Scan ghi
    /// nhận khác rỗng") và cho lúc ép về 0 khi chuyển sang SCAN MODE. SCAN MODE lái bit này qua xung CMD_START
    /// (<see cref="PlcPollingService.PulseCommandAsync"/>), không đi qua đây. Cache-first, nuốt lỗi + log.</summary>
    private async Task PushScanCodeReadyBitSafeAsync(bool value)
    {
        try
        {
            await _polling.WriteParamBitAsync("PARAM_SCAN_CODE_READY", value);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Không thể ghi PARAM_SCAN_CODE_READY xuống PLC");
        }
    }

    public void AddNotification(string message)
    {
        NotificationHistory.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (NotificationHistory.Count > 50) NotificationHistory.RemoveAt(NotificationHistory.Count - 1);
    }
}
