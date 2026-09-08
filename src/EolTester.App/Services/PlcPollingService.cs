using System.Collections.ObjectModel;
using System.Windows.Threading;
using EolTester.App.ViewModels.Rows;
using EolTester.Communication;
using EolTester.Communication.Mc;
using EolTester.Communication.ModbusRtu;
using EolTester.Communication.Slmp;
using EolTester.Configuration;
using EolTester.Configuration.Models;
using EolTester.Core;
using EolTester.Core.Enums;
using EolTester.Core.Models;
using Serilog;

namespace EolTester.App.Services;

/// <summary>
/// Sở hữu duy nhất kết nối PLC và vòng lặp polling nền; đẩy dữ liệu sang UI thread qua Dispatcher.
/// Không ViewModel nào được gọi trực tiếp vào IPlcCommunicationDriver ngoài lớp này. Driver cụ thể (Modbus
/// RTU/MC Protocol/SLMP) được chọn qua <see cref="IPlcCommunicationDriverFactory"/> theo
/// <see cref="ConnectionSettings.Protocol"/> — xem CLAUDE.md mục 6.
/// </summary>
public sealed class PlcPollingService : IAsyncDisposable
{
    private readonly IPlcCommunicationDriverFactory _driverFactory;
    private IPlcCommunicationDriver? _driver;
    private readonly IIoMapStore _ioMapStore;
    private readonly ILanguageService _language;
    private readonly PlcRegisterImage _registerTable;
    private readonly IConnectionSettingsStore _settingsStore;
    private readonly ISpecRegisterMapSource _registerMapSource;
    private readonly Dispatcher _dispatcher;
#if DEBUG
    private readonly Random _random = new();
#endif
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private CancellationTokenSource? _uiRefreshCts;
    private Task? _uiRefreshTask;
    private int _inputBlockStart = 1000;
    private int _inputBlockCount = 20;
    private int _outputBlockStart = 1100;
    private int _outputBlockCount = 20;
    private int _pollingIntervalMs = 500;
    private const int MinPulseMs = 200;
    /// <summary>Giới hạn cứng của Modbus RTU FC03 (đọc nhiều thanh ghi) — request lớn hơn phải chia nhỏ.</summary>
    private const int MaxReadChunk = 125;
    /// <summary>Giới hạn cứng của Modbus RTU FC16 (ghi nhiều thanh ghi) — request lớn hơn phải chia nhỏ.</summary>
    private const int MaxWriteChunk = 123;
    private readonly Dictionary<string, DateTime> _pressTimestamps = new();
    private IReadOnlyDictionary<string, string>? _specAddressMap;
    private string? _led1Address;
    private string? _led2Address;
    private string? _led3Address;
    private string? _productDetectAddress;
    private string? _ngDetectedAddress;
    private string? _scanConfirmAddress;
    private bool _prevScanConfirmValue;
    private string? _csvWriteAddress;
    private bool _prevCsvWriteValue;
    private string? _machineWaitingAddress;
    private string? _cmdStartAddress;

    public ObservableCollection<IoPointRowViewModel> Inputs { get; } = [];
    public ObservableCollection<IoPointRowViewModel> Outputs { get; } = [];
    public ObservableCollection<TestStepRowViewModel> ActiveSteps { get; } = [];

    /// <summary>4 tín hiệu bit cố định hiển thị ở khối "TÍN HIỆU LED"/"SENSOR PHÁT HIỆN SP" tab Main — địa
    /// chỉ định nghĩa qua spec-register-map.csv (khóa SIGNAL_LED1/SIGNAL_LED2/SIGNAL_LED3/SIGNAL_PRODUCT_DETECT),
    /// khác với Inputs/Outputs (nguồn từ io-map.json, dùng cho tab Monitor).</summary>
    public SignalIndicatorState Led1 { get; } = new();
    public SignalIndicatorState Led2 { get; } = new();
    public SignalIndicatorState Led3 { get; } = new();
    public SignalIndicatorState ProductDetected { get; } = new();

    /// <summary>Giá trị thanh ghi "Phát hiện Hàng NG" ở footer dùng chung (khóa SIGNAL_NG_DETECTED trong
    /// spec-register-map.csv) — quy ước 1=xanh, 2=cam, giá trị khác/null=xám (xem ShellViewModel).</summary>
    public RegisterValueState NgDetected { get; } = new();

    /// <summary>PLC báo đã xác nhận mã vừa quét (khóa SIGNAL_SCAN_CONFIRM trong spec-register-map.csv) — bắn
    /// đúng 1 lần khi bit chuyển 0→1 (edge-triggered, PLC tự tắt lại sau đó, PC chỉ đọc không cần ghi trả lời).
    /// ShellViewModel subscribe để "move" ScanBarcodeText hiện tại sang LastScanOk (CommitScan), tách biệt với
    /// đường xác nhận qua UI (Enter/prefix/độ dài, xem KeyboardWedgeScanCapture) — đây là đường xác nhận từ
    /// PLC thật.</summary>
    public event EventHandler? ScanConfirmSignalRaised;

    /// <summary>PLC báo đã sẵn sàng để ghi 1 dòng kết quả ra CSV (khóa SIGNAL_CSV_WRITE trong
    /// spec-register-map.csv) — cùng pattern edge-triggered (0→1) với <see cref="ScanConfirmSignalRaised"/>:
    /// PLC tự quyết định thời điểm dữ liệu đã ổn định (VD sau khi 1 chu trình test hoàn tất), PC chỉ đọc, không
    /// tự polling/tự đoán thời điểm. ShellViewModel subscribe để gọi <see cref="ICsvResultExportService.AppendRowAsync"/>.</summary>
    public event EventHandler? CsvWriteSignalRaised;

    /// <summary>Raise sau mỗi lần ghi 1 thanh ghi cấu hình (tham số/giới hạn) — ShellViewModel subscribe để
    /// debounce lưu <c>runtime-state.json</c> (tính năng an toàn: nhớ giá trị gần nhất qua lần khởi động lại
    /// kể cả khi app tắt không đúng quy trình).</summary>
    public event EventHandler? PersistableStateChanged;

    public ConnectionState State => _driver?.State ?? ConnectionState.Disconnected;
    public event EventHandler<ConnectionState>? StateChanged;

    public PlcPollingService(IPlcCommunicationDriverFactory driverFactory, IIoMapStore ioMapStore, ILanguageService language, PlcRegisterImage registerTable, IConnectionSettingsStore settingsStore, ISpecRegisterMapSource registerMapSource, Dispatcher dispatcher)
    {
        _driverFactory = driverFactory;
        _ioMapStore = ioMapStore;
        _language = language;
        _registerTable = registerTable;
        _settingsStore = settingsStore;
        _registerMapSource = registerMapSource;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Cập nhật phạm vi Input Block dùng cho vòng lặp polling — gọi khi Setup tab lưu cấu hình mới,
    /// để có hiệu lực ngay không cần khởi động lại app (đúng pattern <see cref="ReloadIoMapAsync"/>).
    /// </summary>
    public void SetInputBlockRange(int startAddress, int registerCount)
    {
        _inputBlockStart = startAddress;
        _inputBlockCount = registerCount;
    }

    /// <summary>Cập nhật phạm vi Output Block dùng cho vòng ghi theo chu kỳ — gọi khi Setup tab lưu cấu hình mới.</summary>
    public void SetOutputBlockRange(int startAddress, int registerCount)
    {
        _outputBlockStart = startAddress;
        _outputBlockCount = registerCount;
    }

    /// <summary>Cập nhật khoảng nghỉ giữa 2 tick polling — gọi khi Setup tab lưu cấu hình mới, có hiệu lực
    /// ngay từ tick kế tiếp (không cần <see cref="RestartConnectionAsync"/>, cùng pattern <see cref="SetInputBlockRange"/>).
    /// Kẹp tối thiểu 10ms để tránh vòng lặp bận rộn (busy loop) nếu Admin lỡ nhập 0/số âm.</summary>
    public void SetPollingIntervalMs(int intervalMs)
    {
        _pollingIntervalMs = Math.Max(intervalMs, 10);
    }

    public void SetActiveSteps(IEnumerable<TestStepRowViewModel> steps)
    {
        ActiveSteps.Clear();
        foreach (var step in steps) ActiveSteps.Add(step);
    }

    /// <summary>
    /// Nạp danh sách I/O cho tab Monitor hiển thị — cần dù Role nào (Master hay Slave), vì đây thuần túy
    /// là dữ liệu cấu hình để hiện UI, không liên quan tới việc có polling chủ động hay không.
    /// </summary>
    public async Task LoadIoMapOnlyAsync(CancellationToken ct = default)
    {
        var ioMap = await _ioMapStore.LoadAsync(ct);
        foreach (var point in ioMap.Points.Where(p => p.Direction == IoDirection.Input).OrderBy(p => p.Order))
        {
            Inputs.Add(new IoPointRowViewModel(point, _language));
        }
        foreach (var point in ioMap.Points.Where(p => p.Direction == IoDirection.Output).OrderBy(p => p.Order))
        {
            Outputs.Add(new IoPointRowViewModel(point, _language));
        }

        // Địa chỉ các tín hiệu bit ở tab Main — nạp cùng lúc với I/O Map vì cần dù Role nào (Master/Slave),
        // giống lý do LoadIoMapOnlyAsync tồn tại (thuần dữ liệu cấu hình để hiện UI).
        _specAddressMap ??= await _registerMapSource.LoadAsync(ct);
        _led1Address = _specAddressMap.GetValueOrDefault("SIGNAL_LED1");
        _led2Address = _specAddressMap.GetValueOrDefault("SIGNAL_LED2");
        _led3Address = _specAddressMap.GetValueOrDefault("SIGNAL_LED3");
        _productDetectAddress = _specAddressMap.GetValueOrDefault("SIGNAL_PRODUCT_DETECT");
        _ngDetectedAddress = _specAddressMap.GetValueOrDefault("SIGNAL_NG_DETECTED");
        _scanConfirmAddress = _specAddressMap.GetValueOrDefault("SIGNAL_SCAN_CONFIRM");
        _csvWriteAddress = _specAddressMap.GetValueOrDefault("SIGNAL_CSV_WRITE");
        _machineWaitingAddress = _specAddressMap.GetValueOrDefault("SIGNAL_MACHINE_WAITING");
        _cmdStartAddress = _specAddressMap.GetValueOrDefault("CMD_START");
    }

    /// <summary>
    /// SCAN MODE (xem ShellViewModel.CommitScanAsync/KeyboardWedgeScanCapture): máy sẵn sàng nhận 1 lần quét
    /// mới khi SIGNAL_MACHINE_WAITING (D72.0)=0 VÀ CMD_START (D100, đọc từ cache — PC tự biết giá trị mình vừa
    /// ghi, không cần đọc thật từ PLC vì D100 thuộc dải PC-ghi) = 0. Đọc thuần từ cache
    /// (<see cref="PlcRegisterImage"/>, đã được PollLoopAsync/ModbusSlaveService làm mới liên tục), không có
    /// I/O — gọi được đồng bộ từ KeyboardWedgeScanCapture (mỗi phím gõ). Chưa có dữ liệu xác nhận (2 địa chỉ
    /// chưa từng đọc/ghi được) → mặc định KHÔNG sẵn sàng, tránh tự gửi Start dựa trên dữ liệu chưa xác nhận.
    /// </summary>
    public bool IsMachineReadyForScan()
    {
        var waitingOk = !string.IsNullOrWhiteSpace(_machineWaitingAddress) &&
            _registerTable.TryGetValue(_machineWaitingAddress, out var waiting) && waiting == 0;
        var startOk = !string.IsNullOrWhiteSpace(_cmdStartAddress) &&
            _registerTable.TryGetValue(_cmdStartAddress, out var start) && start == 0;
        return waitingOk && startOk;
    }

    /// <summary>True nếu app đang chạy Role=Master và <see cref="StartAsync"/> đã chạy xong trong phiên này —
    /// dùng để quyết định có gọi <see cref="RestartConnectionAsync"/> (áp dụng ComPort/Baud/... mới ngay,
    /// không cần khởi động lại app) khi Admin bấm "Lưu cấu hình" hay không. Cố ý KHÔNG dựa vào giá trị Role
    /// đang gõ dở trên form Setup — phản ánh đúng trạng thái runtime thật của phiên app hiện tại (đổi Role
    /// vẫn luôn cần khởi động lại, xem Setup_RoleChangeRestartHint).</summary>
    public bool IsActiveMaster { get; private set; }

    /// <summary>Chỉ dùng khi Role=Master — kết nối driver chủ động và bắt đầu vòng lặp polling.</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        await LoadIoMapOnlyAsync(ct);
        await ConnectAndPollAsync(ct);
        IsActiveMaster = true;
    }

    /// <summary>
    /// Chỉ dùng khi Role=Slave — không có driver chủ động/PollLoopAsync (PC không polling PLC), nhưng UI vẫn
    /// cần làm mới định kỳ từ PlcRegisterImage (xem <see cref="RefreshUiFromCacheAsync"/>): dữ liệu trong
    /// cache có thể đổi bất cứ lúc nào do ModbusSlaveService nhận ghi từ PLC Master thật, hoặc do Admin gõ tay
    /// qua "Giám sát DATA" ở tab Setup — không có nơi nào khác tự đẩy các thay đổi đó ra UI. Gọi sau
    /// <see cref="LoadIoMapOnlyAsync"/> ở App.xaml.cs nhánh Role=Slave.
    /// </summary>
    public Task StartUiRefreshOnlyAsync(CancellationToken ct = default)
    {
        _uiRefreshCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _uiRefreshTask = UiRefreshLoopAsync(_uiRefreshCts.Token);
        return Task.CompletedTask;
    }

    private async Task UiRefreshLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RefreshUiFromCacheAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lỗi làm mới UI từ cache (Role=Slave) trong 1 tick — bỏ qua, thử lại ở tick kế tiếp");
            }

            await Task.Delay(500, ct).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    /// <summary>
    /// Dừng vòng lặp polling + ngắt driver đang dùng, rồi đọc lại connection.json và kết nối lại từ đầu —
    /// cho phép đổi ComPort/Baud/Slave ID/.../v.v. có hiệu lực NGAY khi Admin bấm "Lưu cấu hình" ở tab Setup,
    /// không cần khởi động lại app. Chỉ gọi khi <see cref="IsActiveMaster"/> đang true (xem
    /// SetupTabViewModel.SaveAsync) — đổi Role (Master/Slave) vẫn luôn cần khởi động lại vì đụng tới cả
    /// ModbusSlaveService/App.xaml.cs, không thể áp dụng lại từ đây.
    /// </summary>
    public async Task RestartConnectionAsync(CancellationToken ct = default)
    {
        await PauseAsync();
        await ConnectAndPollAsync(ct);
    }

    /// <summary>
    /// Dừng vòng lặp polling + ngắt driver, GIẢI PHÓNG cổng COM/kết nối vật lý mà KHÔNG kết nối lại — dùng để
    /// nhường quyền độc chiếm cổng COM cho 1 lần test tạm thời (xem SetupTabViewModel.TestConnectionAsync),
    /// vì <see cref="ModbusRtu.ModbusRtuDriver"/> giữ cổng COM mở liên tục suốt phiên 1 khi đã connect — driver
    /// tạm thời của Test Connection không bao giờ mở được cùng cổng nếu driver Master sống vẫn đang giữ nó
    /// (UnauthorizedAccessException "Access denied", bug thật đã gặp). Gọi <see cref="RestartConnectionAsync"/>
    /// sau đó để khôi phục lại đúng kết nối đã lưu.
    /// </summary>
    public async Task PauseAsync()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            if (_pollingTask is not null)
            {
                try { await _pollingTask; } catch { /* ignore */ }
            }
        }

        try
        {
            if (_driver is not null) await _driver.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lỗi khi ngắt kết nối driver cũ trước khi áp dụng cấu hình mới");
        }
    }

    private EventHandler<ConnectionState>? _driverStateChangedHandler;

    /// <summary>
    /// Chọn driver Master đúng theo <see cref="ConnectionSettings.Protocol"/> qua <see cref="IPlcCommunicationDriverFactory"/>
    /// rồi nạp thông số kết nối tương ứng — đổi Protocol luôn yêu cầu khởi động lại app (đã ghi ở CLAUDE.md),
    /// nên trong 1 phiên app, driver được chọn không đổi giữa các lần gọi lại (VD RestartConnectionAsync sau
    /// khi Admin đổi ComPort/IP ở Setup) — chỉ (un)subscribe lại StateChanged khi thực sự đổi instance driver.
    /// </summary>
    private void SelectAndConfigureDriver(ConnectionSettings settings)
    {
        var driver = _driverFactory.GetDriver(settings.Protocol);
        if (!ReferenceEquals(driver, _driver))
        {
            if (_driver is not null && _driverStateChangedHandler is not null) _driver.StateChanged -= _driverStateChangedHandler;
            _driverStateChangedHandler = (_, state) => _dispatcher.BeginInvoke(() => StateChanged?.Invoke(this, state));
            driver.StateChanged += _driverStateChangedHandler;
            _driver = driver;
        }

        switch (settings.Protocol)
        {
            case ProtocolType.ModbusRtu:
                ((ModbusRtuDriver)driver).Configure(
                    settings.ComPort,
                    settings.BaudRate,
                    settings.Parity.ToSerialPortParity(),
                    settings.DataBits,
                    settings.StopBits.ToSerialPortStopBits(),
                    (byte)settings.StationAddress,
                    settings.TimeoutMs,
                    settings.RetryCount);
                break;
            case ProtocolType.McProtocol:
                ((McProtocolDriver)driver).Configure(settings.IpAddress, settings.Port, settings.TimeoutMs, settings.RetryCount, settings.McFrameFormat.ToMcFrameFormatKind());
                break;
            case ProtocolType.Slmp:
                ((SlmpDriver)driver).Configure(settings.IpAddress, settings.Port, settings.TimeoutMs, settings.RetryCount);
                break;
            default:
                throw new NotSupportedException($"Giao thức {settings.Protocol} chưa được hỗ trợ ở vai trò Master.");
        }
    }

    /// <summary>Lõi dùng chung cho <see cref="StartAsync"/> và <see cref="RestartConnectionAsync"/>: đọc
    /// connection.json, chọn + cấu hình + kết nối driver Master thật theo Protocol đã cấu hình, rồi khởi động
    /// vòng lặp polling. Tách riêng khỏi StartAsync để RestartConnectionAsync không phải gọi lại
    /// LoadIoMapOnlyAsync (tránh nhân đôi Inputs/Outputs).</summary>
    private async Task ConnectAndPollAsync(CancellationToken ct)
    {
        var settings = await _settingsStore.LoadAsync(ct);
        SetInputBlockRange(settings.InputBlock.StartAddress, settings.InputBlock.RegisterCount);
        SetOutputBlockRange(settings.OutputBlock.StartAddress, settings.OutputBlock.RegisterCount);
        SetPollingIntervalMs(settings.PollingIntervalMs);

        SelectAndConfigureDriver(settings);

        try
        {
            await _driver!.ConnectAsync(ct);
        }
        catch (Exception ex)
        {
            // Driver không kết nối được (sai cổng COM/IP:Port, bị chiếm, chưa cắm cáp/PLC tắt...) — không chặn
            // app khởi động, chỉ log + để ConnectionState=Error hiển thị cho operator qua chỉ báo trạng thái
            // dùng chung. Vòng polling vẫn bắt đầu bình thường và sẽ tự thử kết nối lại ở tick kế tiếp
            // (EnsureConnectedAsync bên trong từng driver).
            Log.Error(ex, "Không thể kết nối driver PLC (giao thức {Protocol})", settings.Protocol);
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _pollingTask = PollLoopAsync(_cts.Token);

        try
        {
            // Ưu tiên nạp dữ liệu ngay lúc khởi động thay vì chờ tick đầu tiên của PollLoopAsync (~500ms) — nhưng
            // đây là lời gọi TRỰC TIẾP trong StartAsync (đường code khởi động app, không nằm trong try/catch
            // catch-all của PollLoopAsync), nên nếu driver thật không kết nối được (VD COM4 không tồn tại/PLC
            // chưa cắm) và ném exception ngay từ EnsureConnectedAsync bên trong, phải tự bắt ở đây — nếu không,
            // exception này bay thẳng lên App.OnStartup và crash cả app TRƯỚC KHI MainWindow kịp hiện ra (không
            // có dialog lỗi nào cho operator thấy, "khởi động mà không có phản hồi gì"). Bỏ qua tick đầu này an
            // toàn vì PollLoopAsync sẽ tự thử lại ở tick kế tiếp, đúng tinh thần try/catch quanh ConnectAsync ở trên.
            await RefreshRuntimeStateAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lỗi đọc/ghi PLC ngay lúc khởi động — bỏ qua, PollLoopAsync sẽ tự thử lại");
        }
    }

    /// <summary>
    /// Nạp lại toàn bộ bản đồ I/O từ IIoMapStore (sau khi Import CSV ghi đè io-map.json) và dựng lại
    /// Inputs/Outputs TẠI CHỖ (Clear rồi Add) — không thay instance collection vì MonitorTabViewModel
    /// đang trỏ thẳng vào cùng 2 collection này. Chạy trên UI thread qua Dispatcher.Invoke để không đụng
    /// độ với PollLoopAsync đang enumerate Inputs ở thread nền.
    /// </summary>
    public async Task ReloadIoMapAsync(CancellationToken ct = default)
    {
        var ioMap = await _ioMapStore.LoadAsync(ct);
        _dispatcher.Invoke(() =>
        {
            Inputs.Clear();
            foreach (var point in ioMap.Points.Where(p => p.Direction == IoDirection.Input).OrderBy(p => p.Order))
            {
                Inputs.Add(new IoPointRowViewModel(point, _language));
            }

            Outputs.Clear();
            foreach (var point in ioMap.Points.Where(p => p.Direction == IoDirection.Output).OrderBy(p => p.Order))
            {
                Outputs.Add(new IoPointRowViewModel(point, _language));
            }
        });

        await RefreshRuntimeStateAsync(ct);
    }

    /// <summary>
    /// Ghi giá trị ngõ ra. Nếu điểm đã cấu hình <c>CommandAddress</c> (Bit 2 — lệnh) thì ghi vào đó; chưa cấu
    /// hình thì fallback ghi thẳng vào <c>Address</c> như trước (tương thích ngược cho các điểm chưa nâng cấp
    /// lên 3-bit — xem CLAUDE.md mục 7 Tab 2). **Bit 1/Bit 2/Bit 3 là 3 vùng độc lập** (xác nhận lại với người
    /// dùng): Bit 1/Bit 3 chỉ PLC được ghi (PC chỉ đọc), Bit 2 chỉ PC được ghi (PLC đọc) — hàm này KHÔNG được
    /// phép tự ý ghi/mirror sang Bit 1. <c>point.Value</c> (bind màu cột "Điểm",
    /// phản ánh Bit 1) chỉ được cập nhật lạc quan cho điểm CHƯA nâng cấp CommandAddress riêng — ở điểm đó
    /// `Address` chính là bit duy nhất đang ghi, không có Bit 1 tách biệt; điểm đã có CommandAddress thì
    /// `point.Value` chỉ đổi qua polling Bit 1 thật. <c>point.CommandValue</c> (bind màu 2 nút ON/OFF, phản
    /// ánh Bit 2) luôn được cập nhật lạc quan ngay ở đây — vì Bit 2 do chính PC ghi, PC luôn biết chắc giá trị
    /// vừa gửi, không có độ trễ/không chắc chắn như Bit 1 (vốn do PLC làm chủ).
    /// </summary>
    public async Task WriteOutputAsync(IoPointRowViewModel point, bool value, CancellationToken ct = default)
    {
        var commandAddress = point.Definition.CommandAddress;
        var hasCommandAddress = !string.IsNullOrWhiteSpace(commandAddress);
        var trackingKey = $"output:{point.Key}";

        if (value) RecordPress(trackingKey);
        else await DelayForMinPulseAsync(trackingKey, ct);

        await WriteBitAsync(hasCommandAddress ? commandAddress! : point.Definition.Address, value, ct);

        if (!hasCommandAddress)
        {
            point.Value = value;
        }
        point.CommandValue = value;
    }

    /// <summary>
    /// "Bumpless transfer" khi chuyển Auto→Manual: với mỗi điểm Output đã cấu hình đủ CommandAddress (Bit 2)
    /// lẫn HandoverAddress (Bit 3), đọc giá trị PLC đang tự quản lý ở Bit 3 rồi ghi đè vào Bit 2 — để lệnh
    /// Thủ công ban đầu khớp đúng trạng thái thật, tránh ngõ ra nhảy trạng thái đột ngột lúc giao quyền điều
    /// khiển. Gọi đúng 1 lần tại thời điểm chuyển đổi (do MonitorTabViewModel.SetModeAsync gọi) — không tự
    /// lặp lại sau đó. Điểm thiếu 1 trong 2 địa chỉ thì bỏ qua (không lỗi). Chỉ đọc Bit 3, ghi Bit 2 — KHÔNG
    /// đụng tới Bit 1 hay <c>point.Value</c>, vì Bit 1 độc lập hoàn toàn với Bit 2/Bit 3 (xem WriteOutputAsync).
    /// Cập nhật <c>point.CommandValue</c> ngay (Bit 2 vừa đổi) để 2 nút ON/OFF phản ánh đúng ngay lúc chuyển
    /// sang Thủ công, không cần chờ tick polling.
    /// </summary>
    public async Task TransferHandoverToCommandAsync(IEnumerable<IoPointRowViewModel> outputs, CancellationToken ct = default)
    {
        foreach (var point in outputs)
        {
            var def = point.Definition;
            if (string.IsNullOrWhiteSpace(def.CommandAddress) || string.IsNullOrWhiteSpace(def.HandoverAddress)) continue;

            var handoverValue = await ReadBitAsync(def.HandoverAddress, ct);
            await WriteBitAsync(def.CommandAddress, handoverValue, ct);
            point.CommandValue = handoverValue;
        }
    }

    /// <summary>Ghi nhận thời điểm bắt đầu giữ (ghi true) — dùng để tính độ rộng xung tối thiểu khi nhả/ghi false.</summary>
    private void RecordPress(string trackingKey) => _pressTimestamps[trackingKey] = DateTime.UtcNow;

    /// <summary>Nếu thời gian giữ chưa đủ MinPulseMs, chờ nốt phần còn thiếu trước khi cho phép ghi false —
    /// đảm bảo PLC luôn thấy xung đủ rộng để quét thấy, kể cả khi người dùng bấm/thả cực nhanh (áp dụng cho
    /// cả nút dạng giữ/thả lẫn click thường, vì click cũng là 1 lần "giữ" rất ngắn).</summary>
    private async Task DelayForMinPulseAsync(string trackingKey, CancellationToken ct)
    {
        if (_pressTimestamps.TryGetValue(trackingKey, out var pressedAt))
        {
            var elapsedMs = (DateTime.UtcNow - pressedAt).TotalMilliseconds;
            if (elapsedMs < MinPulseMs) await Task.Delay(MinPulseMs - (int)elapsedMs, ct);
            _pressTimestamps.Remove(trackingKey);
        }
    }

    /// <summary>Đọc 1 bit theo địa chỉ "Dxxxx.b" (qua PlcRegisterImage, chỉ đọc trực tiếp driver nếu chưa
    /// có trong bảng VÀ đang Role=Master — ở Slave không có driver chủ động, cache là nguồn duy nhất) hoặc
    /// địa chỉ discrete kiểu cũ (chỉ hoạt động ở Master).</summary>
    private async Task<bool> ReadBitAsync(string address, CancellationToken ct)
    {
        if (ModbusWordAddress.TryParse(address, out var addr))
        {
            if (!_registerTable.TryGetWord(addr.WordAddress, out var word))
            {
                if (!IsActiveMaster) return false;
                try
                {
                    var raw = await _driver!.ReadRegisterAsync(new IoAddress(addr.WordAddress.ToString()), ct);
                    word = (ushort)raw;
                    _registerTable.TryStoreReadValue(addr.WordAddress, word);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Log.Error(ex, "Không thể đọc thanh ghi D{Address} — driver có thể đã mất kết nối", addr.WordAddress);
                    return false;
                }
            }

            return addr.BitIndex is int bit ? (word & (1 << bit)) != 0 : word != 0;
        }

        if (!IsActiveMaster) return false;
        return await _driver!.ReadDiscreteAsync(new IoAddress(address), ct);
    }

    /// <summary>
    /// Ghi 1 bit theo địa chỉ "Dxxxx"/"Dxxxx.b" — Modbus không ghi được 1 bit riêng nên phải đọc-sửa-ghi lại
    /// nguyên cả từ (chiến lược "PC sở hữu riêng từ đó"; nếu sau này từ dùng chung với PLC cần đổi sang
    /// read-modify-write thận trọng hơn, xem CLAUDE.md mục 13) — hoặc ghi rời rạc nếu là địa chỉ kiểu cũ.
    /// <b>Luôn cập nhật cache trước</b> (kể cả Role=Slave/mất kết nối — PC là Slave vẫn cần giữ đúng giá trị để
    /// ModbusSlaveService trả lời PLC Master thật hỏi tới); chỉ gửi vật lý xuống driver khi đang chủ động làm
    /// Master (<see cref="IsActiveMaster"/>) — trước đây luôn gọi thẳng driver bất kể Role, khiến mọi lệnh
    /// (Start/Stop/Reset/Xác nhận NG/giới hạn Set spec...) không có tác dụng gì khi Role=Slave vì driver chưa
    /// từng kết nối (ném exception, bị nuốt, cache cũng không được cập nhật theo).
    /// </summary>
    private async Task WriteBitAsync(string address, bool value, CancellationToken ct)
    {
        if (ModbusWordAddress.TryParse(address, out var addr))
        {
            ushort newWord;
            if (addr.BitIndex is int bit)
            {
                var currentWord = await ReadCurrentWordForBitWriteAsync(addr.WordAddress, ct);
                newWord = value ? (ushort)(currentWord | (1 << bit)) : (ushort)(currentWord & ~(1 << bit));
            }
            else
            {
                newWord = (ushort)(value ? 1 : 0);
            }

            _registerTable.TrySetWordForMasterWrite(addr.WordAddress, newWord);

            if (!IsActiveMaster) return;
            try
            {
                await _driver!.WriteRegisterAsync(new IoAddress(addr.WordAddress.ToString()), newWord, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Log.Error(ex, "Không thể gửi lệnh xuống PLC (địa chỉ {Address}) — driver có thể đã mất kết nối", address);
            }
        }
        else
        {
            if (!IsActiveMaster) return;
            try
            {
                await _driver!.WriteDiscreteAsync(new IoAddress(address), value, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Log.Error(ex, "Không thể gửi lệnh xuống PLC (địa chỉ {Address}) — driver có thể đã mất kết nối", address);
            }
        }
    }

    /// <summary>Đọc từ hiện tại để làm nền ghi-đè-1-bit trong WriteBitAsync — ưu tiên cache, chỉ hỏi driver
    /// thật nếu đang Master và cache chưa có; lỗi/không có driver thì coi như 0 (word mới hoàn toàn).</summary>
    private async Task<ushort> ReadCurrentWordForBitWriteAsync(int wordAddress, CancellationToken ct)
    {
        if (_registerTable.TryGetWord(wordAddress, out var cached)) return cached;
        if (!IsActiveMaster) return 0;

        try
        {
            var raw = await _driver!.ReadRegisterAsync(new IoAddress(wordAddress.ToString()), ct);
            return (ushort)raw;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Log.Error(ex, "Không thể đọc thanh ghi D{Address} trước khi ghi 1 bit — driver có thể đã mất kết nối", wordAddress);
            return 0;
        }
    }

    /// <summary>
    /// Ghi thẳng 1 thanh ghi theo địa chỉ số nguyên — dùng cho "Giám sát thanh ghi" ở tab Setup khi Admin
    /// chỉnh sửa giá trị thủ công (đặc biệt hữu ích với thanh ghi Output khi PC làm Master). Chỉ gọi khi
    /// Role=Master (driver đã kết nối); ở Slave, chỉ cần cập nhật PlcRegisterImage — xem SetupTabViewModel.
    /// </summary>
    public async Task WriteRawWordAsync(int wordAddress, ushort value, CancellationToken ct = default)
    {
        if (_driver is null) return;
        try
        {
            await _driver.WriteRegisterAsync(new IoAddress(wordAddress.ToString()), value, ct);
            _registerTable.TrySetWordForMasterWrite(wordAddress, value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Cùng lý do với WriteBitAsync — không để mất kết nối giữa chừng crash app.
            Log.Error(ex, "Không thể ghi thanh ghi D{Address} xuống PLC — driver có thể đã mất kết nối", wordAddress);
        }
    }

    /// <summary>
    /// Ghi LowerLimit/UpperLimit của mọi bước đã cấu hình <see cref="TestStepDefinition.LowerLimitAddress"/>/
    /// <see cref="TestStepDefinition.UpperLimitAddress"/> xuống PLC dạng WordSigned — quy đổi raw =
    /// value*Scale (xem <see cref="TestStepDefinition.Scale"/>, EolTester.Core.PlcGainScale), Scale=1 (mặc
    /// định) tương đương quy ước THÔ cũ. Gọi đúng 1 lần khi Admin/Operator bấm "Xác nhận thay đổi" ở tab Set
    /// spec. — KHÔNG realtime theo từng ký tự gõ (xem SettingTabViewModel). Bước thiếu địa chỉ hoặc chưa nhập
    /// giới hạn thì bỏ qua, không lỗi.
    /// </summary>
    public async Task WriteStepLimitsAsync(IEnumerable<TestStepDefinition> steps, CancellationToken ct = default)
    {
        foreach (var def in steps)
        {
            if (!string.IsNullOrWhiteSpace(def.LowerLimitAddress) && def.LowerLimit.HasValue)
            {
                var raw = (short)Math.Round(def.LowerLimit.Value * def.Scale, 0, MidpointRounding.AwayFromZero);
                await WriteWordToAddressAsync(def.LowerLimitAddress!, unchecked((ushort)raw), ct);
            }
            if (!string.IsNullOrWhiteSpace(def.UpperLimitAddress) && def.UpperLimit.HasValue)
            {
                var raw = (short)Math.Round(def.UpperLimit.Value * def.Scale, 0, MidpointRounding.AwayFromZero);
                await WriteWordToAddressAsync(def.UpperLimitAddress!, unchecked((ushort)raw), ct);
            }
        }
        PersistableStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Chụp giá trị các thanh ghi cấu hình trong dải D100-D199 (BỎ các khóa CMD_ — lệnh xung, không
    /// nên "nhớ" qua lần khởi động lại) từ cache, khóa dạng <c>"D104"</c>. Dùng để lưu runtime-state.json.</summary>
    public async Task<Dictionary<string, int>> SnapshotPersistableD1xxRegistersAsync(CancellationToken ct = default)
    {
        _specAddressMap ??= await _registerMapSource.LoadAsync(ct);
        var result = new Dictionary<string, int>();
        foreach (var (key, address) in _specAddressMap)
        {
            if (key.StartsWith("CMD_", StringComparison.Ordinal)) continue;
            if (!ModbusWordAddress.TryParse(address, out var addr)) continue;
            if (addr.WordAddress is < 100 or > 199) continue;

            var wordKey = $"D{addr.WordAddress}";
            if (result.ContainsKey(wordKey)) continue;
            if (_registerTable.TryGetWord(addr.WordAddress, out var word)) result[wordKey] = word;
        }
        return result;
    }

    /// <summary>Nạp lại ảnh chụp <see cref="SnapshotPersistableD1xxRegistersAsync"/> vào cache (chỉ cache — vòng
    /// polling Phase 2 tự đẩy D100-D199 xuống PLC mỗi tick). Chỉ áp cho địa chỉ trong dải D100-D199.</summary>
    public void RestorePersistableD1xxRegisters(IReadOnlyDictionary<string, int> registers)
    {
        foreach (var (wordKey, value) in registers)
        {
            if (!ModbusWordAddress.TryParse(wordKey, out var addr) || addr.BitIndex is not null) continue;
            if (addr.WordAddress is < 100 or > 199) continue;
            _registerTable.TrySetWordForMasterWrite(addr.WordAddress, unchecked((ushort)value));
        }
    }

    /// <summary>Ghi 1 giá trị nguyên (không scale) xuống thanh ghi tra theo khóa logic trong spec-register-map.csv
    /// — dùng cho các tham số cài đặt (độ dài mã scan, thời gian test High/Low, thời gian giữ nút). Bỏ qua im
    /// lặng nếu khóa chưa được định nghĩa trong CSV (tính năng option, không bắt buộc phải có PLC nhận).</summary>
    public async Task WriteParamWordAsync(string logicalKey, int value, CancellationToken ct = default)
    {
        _specAddressMap ??= await _registerMapSource.LoadAsync(ct);
        if (!_specAddressMap.TryGetValue(logicalKey, out var address)) return;

        await WriteWordToAddressAsync(address, unchecked((ushort)value), ct);
        PersistableStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ghi 1 cờ bool xuống thanh ghi/bit tra theo khóa logic trong spec-register-map.csv — dùng cho
    /// "Bắt buộc Scan"/"Bắt buộc đúng thứ tự tem"/"Trạng thái bỏ qua lỗi". Bỏ qua im lặng nếu khóa chưa được
    /// định nghĩa trong CSV.</summary>
    public async Task WriteParamBitAsync(string logicalKey, bool value, CancellationToken ct = default)
    {
        _specAddressMap ??= await _registerMapSource.LoadAsync(ct);
        if (!_specAddressMap.TryGetValue(logicalKey, out var address)) return;

        await WriteBitAsync(address, value, ct);
        PersistableStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ghi thẳng 1 thanh ghi word theo địa chỉ "Dxxxx" (không hậu tố bit) — lõi dùng chung cho
    /// WriteStepLimitsAsync/WriteParamWordAsync. <b>Luôn cập nhật cache trước</b> (kể cả Role=Slave/mất kết
    /// nối), chỉ gửi vật lý xuống driver khi đang chủ động làm Master — cùng lý do với WriteBitAsync (trước
    /// đây luôn gọi thẳng driver bất kể Role, khiến "Xác nhận thay đổi" ở Set spec. không có tác dụng gì khi
    /// Role=Slave).</summary>
    private async Task WriteWordToAddressAsync(string address, ushort rawValue, CancellationToken ct)
    {
        if (!ModbusWordAddress.TryParse(address, out var addr) || addr.BitIndex is not null) return;

        _registerTable.TrySetWordForMasterWrite(addr.WordAddress, rawValue);

        if (!IsActiveMaster) return;
        try
        {
            await _driver!.WriteRegisterAsync(new IoAddress(addr.WordAddress.ToString()), rawValue, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Không thể ghi thanh ghi (địa chỉ {Address}) — driver có thể đã mất kết nối", address);
        }
    }

    /// <summary>
    /// Gửi một tín hiệu dạng xung (bật rồi tắt) xuống PLC — dùng cho các lệnh Start/Stop/Reset/Xác nhận NG.
    /// <paramref name="logicalKey"/> (VD "CMD_START") được tra trong spec-register-map.csv (qua
    /// <see cref="ISpecRegisterMapSource"/>, nạp 1 lần rồi cache) để lấy địa chỉ thanh ghi thật (VD "D1100")
    /// — ghi qua <see cref="WriteBitAsync"/> nên vừa cập nhật <see cref="PlcRegisterImage"/> (hiện ngay ở
    /// "Giám sát DATA") vừa đẩy ra driver. Key không có trong CSV → giữ hành vi cũ (ghi discrete thẳng theo
    /// chuỗi khóa, tương thích ngược).
    /// <para>
    /// Riêng khi <paramref name="logicalKey"/> = "CMD_START" VÀ <paramref name="scanCodeReadyForThisStart"/>
    /// != null (chỉ SCAN MODE truyền — REV MODE để null vì bit đó do <c>ShellViewModel</c> lái theo mức
    /// LastScanOk), bit PARAM_SCAN_CODE_READY (D104.2) được gắn vào vòng đời xung CMD_START: nâng lên 1 CÙNG
    /// LÚC CMD_START lên — chỉ khi <paramref name="scanCodeReadyForThisStart"/> = true (lần Start này có "Mã
    /// Scan ghi nhận" hợp lệ đi kèm) — và LUÔN hạ về 0 khi CMD_START tắt (dù true hay false), kể cả khi bit
    /// đang treo 1. Ô "Mã Scan ghi nhận" ở footer giữ nguyên hiển thị khi bit này hạ.
    /// </para>
    /// </summary>
    public async Task PulseCommandAsync(string logicalKey, int pulseMs = 200, bool? scanCodeReadyForThisStart = null, CancellationToken ct = default)
    {
        _specAddressMap ??= await _registerMapSource.LoadAsync(ct);
        var address = _specAddressMap.TryGetValue(logicalKey, out var mapped) ? mapped : logicalKey;

        var scanReadyAddress = logicalKey == "CMD_START" && scanCodeReadyForThisStart.HasValue
            && _specAddressMap.TryGetValue("PARAM_SCAN_CODE_READY", out var sra)
            ? sra
            : null;

        await WriteBitAsync(address, true, ct);
        if (scanReadyAddress is not null && scanCodeReadyForThisStart == true) await WriteBitAsync(scanReadyAddress, true, ct);

        await Task.Delay(pulseMs, ct);

        await WriteBitAsync(address, false, ct);
        if (scanReadyAddress is not null) await WriteBitAsync(scanReadyAddress, false, ct);
    }

    /// <summary>Ghi mức tín hiệu (không tự đảo về false như PulseCommandAsync) — dùng cho các lệnh kiểu
    /// "giữ = ON, thả = OFF" (Reset, Xác nhận NG) và mức Auto/Manual. Tra spec-register-map.csv giống
    /// PulseCommandAsync, ghi qua WriteBitAsync (Dxxxx thật) — key không có trong CSV thì fallback ghi discrete
    /// thẳng theo chuỗi khóa (tương thích ngược, cùng pattern PulseCommandAsync). Tự đảm bảo xung tối thiểu
    /// MinPulseMs khi ghi false ngay sau khi vừa ghi true.</summary>
    public async Task SetLevelCommandAsync(string logicalKey, bool value, CancellationToken ct = default)
    {
        _specAddressMap ??= await _registerMapSource.LoadAsync(ct);
        var address = _specAddressMap.TryGetValue(logicalKey, out var mapped) ? mapped : logicalKey;

        if (value) RecordPress(logicalKey);
        else await DelayForMinPulseAsync(logicalKey, ct);

        await WriteBitAsync(address, value, ct);
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RefreshRuntimeStateAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                // Inputs vừa bị Clear()/Add() lại bởi ReloadIoMapAsync (sau Import CSV) trong lúc đang
                // enumerate — bỏ qua tick này, vòng lặp tiếp theo sẽ đọc đúng danh sách mới.
            }
            catch (Exception ex)
            {
                // Driver thật (cổng COM mất kết nối, PLC không phản hồi kịp timeout, cáp rút...) ném ra nhiều loại
                // exception khác nhau (TimeoutException, IOException, InvalidOperationException từ NModbus...) —
                // bọc chung 1 catch-all ở đây để 1 lần đọc/ghi lỗi không làm CHẾT HẲN vòng lặp polling nền (task
                // không ai await/observe exception cho tới DisposeAsync). ConnectionState của driver đã tự chuyển
                // Error ngay tại nơi ném lỗi (xem ModbusRtuDriver) — tick kế tiếp sẽ tự thử kết nối lại
                // (EnsureConnectedAsync), đóng vai trò backoff ~500ms tự nhiên, không cần thêm cơ chế retry riêng.
                Log.Error(ex, "Lỗi đọc/ghi PLC trong 1 tick polling — bỏ qua, thử lại ở tick kế tiếp");
            }

            await Task.Delay(_pollingIntervalMs, ct).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    private async Task RefreshRuntimeStateAsync(CancellationToken ct)
    {
        // Phase 1+2 (vật lý, cần driver đang kết nối thật) được cô lập trong try/catch riêng — nếu PLC mất kết
        // nối, các vòng đọc/ghi này ném exception ngay ở request đầu tiên; TRƯỚC ĐÂY exception đó bay thẳng lên
        // PollLoopAsync và HỦY LUÔN phần cập nhật UI từ cache bên dưới (Inputs/Outputs/Led/ActiveSteps) — hậu quả
        // là Admin ghi tay giá trị qua "Giám sát DATA" (ForceSetWordForAdminOverride, luôn thành công vào cache
        // bất kể kết nối) không bao giờ được hiển thị ra UI khi đang mất kết nối, dù cache đã có giá trị đúng.
        // Giờ lỗi vật lý chỉ dừng phase đọc/ghi đó, phần cập nhật UI từ cache phía dưới luôn chạy mỗi tick.
        try
        {
            if (_driver is null) return; // Không có driver Master đang chọn (VD Role=Slave gọi nhầm) — bỏ qua phần vật lý.

            // Phase 1: đọc từ PLC tới PC cho D0..D99 và cho phạm vi Input Block từ Setup (D200+) — gộp thành
            // request KHỐI (nhiều thanh ghi/lần, FC03) thay vì 1 request/thanh ghi. TRƯỚC ĐÂY vòng này đọc/ghi
            // TỪNG thanh ghi riêng lẻ (D0-D99 = 100 request, D100-D199 = 100 request, cộng Input/Output Block
            // mặc định 20+20 — tổng ~240 giao dịch Modbus TUẦN TỰ mỗi tick) — mỗi giao dịch là 1 round-trip
            // request/response thật trên bus RS485 half-duplex, khiến 1 tick thực tế mất VÀI GIÂY dù
            // Task.Delay giữa 2 tick chỉ 500ms — đây là nguyên nhân thật của độ trễ hiển thị PLC→PC ở "Giám
            // sát DATA" (không phải do chu kỳ polling), đã xác nhận qua đọc code + đo thực tế. Giờ mỗi dải cố
            // định/Block chỉ còn 1 request (hoặc vài request nếu vượt giới hạn 125/123 thanh ghi của Modbus).
            await ReadRangeIntoCacheAsync(0, 100, ct);
            await ReadRangeIntoCacheAsync(_inputBlockStart, _inputBlockCount, ct);

            // Phase 2: ghi từ PC xuống PLC cho D100..D199 và phạm vi Output Block từ Setup (D200+) — cùng
            // nguyên tắc gộp khối như Phase 1 (FC16).
            await WriteRangeFromCacheAsync(100, 100, ct);
            await WriteRangeFromCacheAsync(_outputBlockStart, _outputBlockCount, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lỗi đọc/ghi khối D0-D99/Input-Output Block trong 1 tick polling — bỏ qua phần vật lý, vẫn cập nhật UI từ cache bên dưới");
        }

        await RefreshUiFromCacheAsync(ct);
    }

    /// <summary>
    /// Đọc 1 dải địa chỉ liên tiếp vào cache bằng ít request khối nhất có thể. Dải truyền vào có thể chồng lấn
    /// 1 phần với vùng không được phép đọc (VD Input Block cấu hình cũ lỡ đè lên D100-D199 chỉ-ghi, từ trước
    /// khi Setup có validate D200-D2000) — quét theo TỪNG ĐOẠN CON liên tục cùng đọc-được (dùng
    /// <see cref="PlcRegisterImage.CanReadAddress"/>) thay vì bỏ qua từng địa chỉ lẻ như vòng lặp cũ, để trường
    /// hợp bình thường (toàn dải hợp lệ, tuyệt đại đa số) vẫn chỉ tốn 1 request thay vì N.
    /// </summary>
    private async Task ReadRangeIntoCacheAsync(int startAddress, int count, CancellationToken ct)
    {
        var segmentStart = -1;
        for (var i = 0; i <= count; i++)
        {
            var addr = startAddress + i;
            var readable = i < count && _registerTable.CanReadAddress(addr);
            if (readable && segmentStart < 0) segmentStart = addr;
            else if (!readable && segmentStart >= 0)
            {
                await ReadChunkedAsync(segmentStart, addr - segmentStart, ct);
                segmentStart = -1;
            }
        }
    }

    /// <summary>Đọc đúng 1 đoạn liên tục đã xác nhận hợp lệ toàn bộ, tự chia nhỏ theo <see cref="MaxReadChunk"/>
    /// nếu vượt giới hạn 1 request Modbus FC03 (125 thanh ghi).</summary>
    private async Task ReadChunkedAsync(int startAddress, int count, CancellationToken ct)
    {
        var offset = 0;
        while (offset < count)
        {
            var chunkCount = Math.Min(MaxReadChunk, count - offset);
            var chunkStart = startAddress + offset;
            var values = await _driver!.ReadRegistersAsync(new IoAddress(chunkStart.ToString()), chunkCount, ct);
            var words = new ushort[values.Length];
            for (var i = 0; i < values.Length; i++) words[i] = (ushort)values[i];
            _registerTable.StoreReadBlock(chunkStart, words);
            offset += chunkCount;
        }
    }

    /// <summary>Đối xứng với <see cref="ReadRangeIntoCacheAsync"/> cho chiều ghi — chỉ ghi những đoạn con thực
    /// sự được phép ghi (<see cref="PlcRegisterImage.CanWriteAddress"/>).</summary>
    private async Task WriteRangeFromCacheAsync(int startAddress, int count, CancellationToken ct)
    {
        var segmentStart = -1;
        for (var i = 0; i <= count; i++)
        {
            var addr = startAddress + i;
            var writable = i < count && _registerTable.CanWriteAddress(addr);
            if (writable && segmentStart < 0) segmentStart = addr;
            else if (!writable && segmentStart >= 0)
            {
                await WriteChunkedAsync(segmentStart, addr - segmentStart, ct);
                segmentStart = -1;
            }
        }
    }

    /// <summary>Ghi đúng 1 đoạn liên tục đã xác nhận hợp lệ toàn bộ, tự chia nhỏ theo <see cref="MaxWriteChunk"/>
    /// nếu vượt giới hạn 1 request Modbus FC16 (123 thanh ghi). Không cần ghi lại cache sau khi gửi — giá trị
    /// ghi xuống driver lấy thẳng từ cache (<see cref="PlcRegisterImage.SnapshotBlock"/>), tự nó đã đúng.</summary>
    private async Task WriteChunkedAsync(int startAddress, int count, CancellationToken ct)
    {
        var offset = 0;
        while (offset < count)
        {
            var chunkCount = Math.Min(MaxWriteChunk, count - offset);
            var chunkStart = startAddress + offset;
            var words = _registerTable.SnapshotBlock(chunkStart, chunkCount);
            var values = new int[words.Length];
            for (var i = 0; i < words.Length; i++) values[i] = words[i];
            await _driver!.WriteRegistersAsync(new IoAddress(chunkStart.ToString()), values, ct);
            offset += chunkCount;
        }
    }

    /// <summary>
    /// Đọc lại <see cref="PlcRegisterImage"/> (không gọi driver vật lý, trừ nhánh fallback địa chỉ kiểu cũ)
    /// để cập nhật Inputs/Outputs/LED/giá trị đo lên UI. Tách riêng khỏi <see cref="RefreshRuntimeStateAsync"/>
    /// vì đây là phần DUY NHẤT còn có ý nghĩa khi Role=Slave — <see cref="PollLoopAsync"/> (và toàn bộ Phase 1/2
    /// vật lý ở trên) chỉ chạy khi Role=Master; ở Slave, ModbusSlaveService (nhận ghi từ PLC Master thật) và
    /// "Giám sát DATA" (Admin gõ tay) đều chỉ cập nhật cache — cần 1 vòng lặp riêng (<see cref="UiRefreshLoopAsync"/>)
    /// đọc lại cache này định kỳ để các thay đổi đó thật sự hiển thị ra UI, không chỉ nằm im trong cache.
    /// </summary>
    private async Task RefreshUiFromCacheAsync(CancellationToken ct)
    {
        foreach (var input in Inputs)
        {
            if (ModbusWordAddress.TryParse(input.Definition.Address, out _))
            {
                if (_registerTable.TryGetValue(input.Definition.Address, out var v))
                {
                    var value = v != 0;
                    _ = _dispatcher.BeginInvoke(() => input.Value = value);
                }
            }
            else
            {
                if (_driver is null) continue; // Role=Slave (hoặc chưa chọn driver Master) — không có driver chủ động để hỏi địa chỉ legacy này.
                try
                {
                    var value = await _driver.ReadDiscreteAsync(new IoAddress(input.Definition.Address), ct);
                    _ = _dispatcher.BeginInvoke(() => input.Value = value);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Log.Error(ex, "Lỗi đọc Input {Key} (địa chỉ legacy {Address}) — bỏ qua, không để hỏng cả tick", input.Key, input.Definition.Address);
                }
            }
        }

        // Bit 1 (trạng thái) đọc cho cả Input lẫn Output — nguồn sự thật DUY NHẤT cho màu "Điểm", hoàn toàn
        // độc lập với Bit 2 (lệnh PC gửi)/Bit 3 (bàn giao) — xem WriteOutputAsync/TransferHandoverToCommandAsync.
        // PC không bao giờ tự ghi Bit 1 — chỉ đổi khi PLC thật tự cập nhật, hoặc lúc test Admin tự ghi qua
        // "Giám sát DATA" ở tab Setup (ghi thẳng PlcRegisterImage, tick polling kế tiếp đọc thấy ngay).
        foreach (var output in Outputs)
        {
            if (ModbusWordAddress.TryParse(output.Definition.Address, out _))
            {
                if (_registerTable.TryGetValue(output.Definition.Address, out var v))
                {
                    var value = v != 0;
                    _ = _dispatcher.BeginInvoke(() => output.Value = value);
                }
            }
            else
            {
                if (_driver is null) continue; // Cùng lý do nhánh Input ở trên.
                try
                {
                    var value = await _driver.ReadDiscreteAsync(new IoAddress(output.Definition.Address), ct);
                    _ = _dispatcher.BeginInvoke(() => output.Value = value);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Log.Error(ex, "Lỗi đọc Output {Key} (địa chỉ legacy {Address}) — bỏ qua, không để hỏng cả tick", output.Key, output.Definition.Address);
                }
            }

            // Bit 2 (lệnh) — poll riêng, độc lập với Bit 1, để màu 2 nút ON/OFF (bind CommandValue) cũng
            // đồng bộ với "Giám sát DATA" (Admin có thể tự ghi thẳng CommandAddress lúc test), không chỉ
            // phụ thuộc vào lần cập nhật lạc quan lúc WriteOutputAsync/TransferHandoverToCommandAsync.
            if (!string.IsNullOrWhiteSpace(output.Definition.CommandAddress) &&
                _registerTable.TryGetValue(output.Definition.CommandAddress, out var cmd))
            {
                var cmdValue = cmd != 0;
                _ = _dispatcher.BeginInvoke(() => output.CommandValue = cmdValue);
            }
            else if (string.IsNullOrWhiteSpace(output.Definition.CommandAddress))
            {
                _ = _dispatcher.BeginInvoke(() => output.CommandValue = output.Value);
            }
        }

        foreach (var step in ActiveSteps)
        {
            var measured = ReadMeasurement(step);
            var okNg = ReadOkNg(step);
            var regLower = ReadRegisterLimit(step.Definition.LowerLimitAddress, step.Definition.Scale);
            var regUpper = ReadRegisterLimit(step.Definition.UpperLimitAddress, step.Definition.Scale);
            _ = _dispatcher.BeginInvoke(() =>
            {
                step.MeasuredValue = measured;
                step.OkNgResult = okNg;
                step.RegisterLowerLimit = regLower;
                step.RegisterUpperLimit = regUpper;
            });
        }

        UpdateSignal(_led1Address, v => Led1.Value = v);
        UpdateSignal(_led2Address, v => Led2.Value = v);
        UpdateSignal(_led3Address, v => Led3.Value = v);
        UpdateSignal(_productDetectAddress, v => ProductDetected.Value = v);
        UpdateWordValue(_ngDetectedAddress, v => NgDetected.Value = v);
        UpdateEdgeSignal(_scanConfirmAddress, ref _prevScanConfirmValue, () => ScanConfirmSignalRaised?.Invoke(this, EventArgs.Empty));
        UpdateEdgeSignal(_csvWriteAddress, ref _prevCsvWriteValue, () => CsvWriteSignalRaised?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Đọc 1 tín hiệu bit đơn (Dxxxx.Y) từ <see cref="PlcRegisterImage"/> vào UI thread — dùng
    /// chung cho Led1/Led2/ProductDetected. Bỏ qua im lặng nếu chưa cấu hình địa chỉ hoặc chưa có dữ liệu.</summary>
    private void UpdateSignal(string? address, Action<bool> setter)
    {
        if (string.IsNullOrWhiteSpace(address)) return;
        if (!ModbusWordAddress.TryParse(address, out _)) return;
        if (!_registerTable.TryGetValue(address, out var raw)) return;

        var value = raw != 0;
        _dispatcher.BeginInvoke(() => setter(value));
    }

    /// <summary>Đọc nguyên giá trị 1 thanh ghi word (không phải bit) từ <see cref="PlcRegisterImage"/> vào
    /// UI thread — dùng cho "Phát hiện Hàng NG" (quy ước nhiều hơn 2 trạng thái: 1/2/khác).</summary>
    private void UpdateWordValue(string? address, Action<int?> setter)
    {
        if (string.IsNullOrWhiteSpace(address)) return;
        if (!ModbusWordAddress.TryParse(address, out var addr) || addr.BitIndex is not null) return;
        if (!_registerTable.TryGetWord(addr.WordAddress, out var raw)) return;

        var value = (int)raw;
        _dispatcher.BeginInvoke(() => setter(value));
    }

    /// <summary>Đọc 1 tín hiệu bit và chỉ gọi <paramref name="onRisingEdge"/> đúng 1 lần khi bit chuyển 0→1 —
    /// khác <see cref="UpdateSignal"/> (level, gọi setter MỌI tick bất kể tăng/giảm), dùng cho tín hiệu kiểu
    /// "xung xác nhận" từ PLC (VD SIGNAL_SCAN_CONFIRM) mà ta chỉ muốn phản ứng đúng 1 lần mỗi lần PLC bật bit,
    /// không phải phản ứng liên tục mỗi tick trong lúc bit vẫn đang giữ 1.</summary>
    private void UpdateEdgeSignal(string? address, ref bool previousValue, Action onRisingEdge)
    {
        if (string.IsNullOrWhiteSpace(address)) return;
        if (!ModbusWordAddress.TryParse(address, out _)) return;
        if (!_registerTable.TryGetValue(address, out var raw)) return;

        var value = raw != 0;
        if (value && !previousValue) _dispatcher.BeginInvoke(onRisingEdge);
        previousValue = value;
    }

    /// <summary>
    /// Đọc giá trị đo cho 1 bước High/Low mode ở tab Main. Nếu bước đã được gán Address (qua Import CSV
    /// ở tab Set Spec.) thì đọc thật từ PlcRegisterImage — diễn giải word là WordSigned, quy đổi raw/Scale
    /// (xem <see cref="TestStepDefinition.Scale"/>) để ra giá trị THỰC, làm tròn đúng số chữ số thập phân
    /// tương ứng Scale (EolTester.Core.PlcGainScale). Trả về null nếu địa chỉ nằm ngoài phạm vi Input/Output
    /// Block đã cấu hình (chưa polling tới) — hiển thị "--" thay vì số sai. Bước chưa gán Address vẫn dùng
    /// dữ liệu giả lập (Debug)/0 (Release) như cũ.
    /// </summary>
    private decimal? ReadMeasurement(TestStepRowViewModel step)
    {
        if (!string.IsNullOrWhiteSpace(step.Definition.Address) &&
            ModbusWordAddress.TryParse(step.Definition.Address, out var addr) && addr.BitIndex is null)
        {
            return _registerTable.TryGetWord(addr.WordAddress, out var raw)
                ? PlcGainScale.Round((decimal)(short)raw / step.Definition.Scale, step.Definition.Scale)
                : null;
        }

        return GenerateDemoMeasurement(step);
    }

    /// <summary>
    /// Đọc trạng thái OK/NG cho 1 bước từ <see cref="TestStepDefinition.OkNgAddress"/> (word, quy ước
    /// 1=OK/2=NG) — dùng chung cho cột "OK / NG" bổ sung ở mọi bước, và cho cột "Giá trị" của các bước kiểu
    /// kiểm tra tín hiệu (không có giới hạn số, VD "Kiểm tra LED 1"). Trả về null nếu chưa gán địa chỉ hoặc
    /// giá trị đọc được không phải 1/2 (chưa xác định).
    /// </summary>
    private bool? ReadOkNg(TestStepRowViewModel step)
    {
        var address = step.Definition.OkNgAddress;
        if (string.IsNullOrWhiteSpace(address)) return null;
        if (!ModbusWordAddress.TryParse(address, out var addr) || addr.BitIndex is not null) return null;
        if (!_registerTable.TryGetWord(addr.WordAddress, out var raw)) return null;

        return raw switch { 1 => true, 2 => false, _ => (bool?)null };
    }

    /// <summary>Đọc giá trị giới hạn hiện có trong <see cref="PlcRegisterImage"/> ở địa chỉ Lower/UpperLimitAddress
    /// của 1 bước — dùng cùng quy đổi raw/Scale với <see cref="ReadMeasurement"/>, để so sánh với giá trị
    /// Admin/Operator đang gõ ở Set spec. (tô màu cảnh báo khi khác nhau — xem
    /// TestStepRowViewModel.IsLowerLimitPending). Trả về null nếu chưa gán địa chỉ hoặc chưa có dữ liệu trong
    /// cache (chưa polling tới/chưa từng ghi).</summary>
    private decimal? ReadRegisterLimit(string? address, int scale)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        if (!ModbusWordAddress.TryParse(address, out var addr) || addr.BitIndex is not null) return null;
        return _registerTable.TryGetWord(addr.WordAddress, out var raw)
            ? PlcGainScale.Round((decimal)(short)raw / scale, scale)
            : null;
    }

    /// <summary>
    /// Sinh giá trị đo lường giả cho bước chưa gán Address — chỉ ở build Debug, để phát triển/demo UI không
    /// cần phần cứng. Publish Release tự động tắt (trả về 0 cố định), tránh hiện dữ liệu "giả lập" gây nhầm
    /// lẫn khi đưa cho khách hàng xem/đấu nối PLC thật. Xem CLAUDE.md mục 7/16.
    /// </summary>
    private decimal GenerateDemoMeasurement(TestStepRowViewModel step)
    {
#if DEBUG
        var low = step.LowerLimit ?? 0m;
        var high = step.UpperLimit ?? (low + 10m);
        var mid = (low + high) / 2m;
        var spread = (high - low) * 0.15m;
        var value = mid + (decimal)(_random.NextDouble() * 2 - 1) * spread;
        return PlcGainScale.Round(value, step.Definition.Scale);
#else
        return 0m;
#endif
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_pollingTask is not null)
        {
            try { await _pollingTask; } catch { /* ignore */ }
        }
        _uiRefreshCts?.Cancel();
        if (_uiRefreshTask is not null)
        {
            try { await _uiRefreshTask; } catch { /* ignore */ }
        }
        if (_driver is not null) await _driver.DisposeAsync();
    }
}
