using System.Globalization;
using System.IO.Ports;
using System.Windows.Threading;
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

namespace EolTester.App.ViewModels;

public partial class SetupTabViewModel : ObservableObject
{
    private const int MaxRegistersPerModbusRequest = 125;

    /// <summary>
    /// Lưới "Giám sát DATA" cố định 2 cột × 15 dòng (30 ô) — thiết kế sẵn cho người vận hành thao tác,
    /// không phải danh sách thêm/xóa động (đây là màn hình đưa cho khách hàng xem, cần gọn/ổn định).
    /// </summary>
    private const int RegisterWatchRowsPerColumn = 15;

    private readonly IConnectionSettingsStore _settingsStore;
    private readonly IRegisterWatchStore _registerWatchStore;
    private readonly PlcRegisterImage _registerTable;
    private readonly PlcPollingService _pollingService;
    private readonly IAuditLogService _auditLog;
    private readonly IAuthenticationService _auth;
    private readonly DispatcherTimer _watchRefreshTimer;

    // Chuẩn truyền thông chọn được — đặt lên đầu khối cấu hình kết nối (trước cả Vai trò Master/Slave), theo
    // đúng yêu cầu người dùng. Chỉ liệt kê các giao thức đã có driver thật (ModbusRtu/McProtocol/Slmp) —
    // CcLink/EthernetIp còn lại trong enum ProtocolType nhưng chưa cài đặt, không đưa vào danh sách chọn được.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModbusRtu))]
    [NotifyPropertyChangedFor(nameof(IsMcOrSlmp))]
    [NotifyPropertyChangedFor(nameof(IsMcProtocol))]
    private ProtocolType _protocol = ProtocolType.ModbusRtu;

    public bool IsModbusRtu => Protocol == ProtocolType.ModbusRtu;
    public bool IsMcOrSlmp => Protocol is ProtocolType.McProtocol or ProtocolType.Slmp;
    public bool IsMcProtocol => Protocol == ProtocolType.McProtocol;

    public IReadOnlyList<ProtocolType> AvailableProtocols { get; } = [ProtocolType.ModbusRtu, ProtocolType.McProtocol, ProtocolType.Slmp];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMasterMode))]
    [NotifyPropertyChangedFor(nameof(IsSlaveMode))]
    private ModbusRole _role = ModbusRole.Master;

    public bool IsMasterMode => Role == ModbusRole.Master;
    public bool IsSlaveMode => Role == ModbusRole.Slave;

    /// <summary>StationAddress/MySlaveId chỉ có ý nghĩa với Modbus RTU (địa chỉ trạm trên bus RS-485) — MC
    /// Protocol/SLMP định danh đích qua IP:Port, không có khái niệm tương đương lộ ra UI ở vòng triển khai này.</summary>
    public bool IsModbusMasterMode => IsModbusRtu && IsMasterMode;
    public bool IsModbusSlaveMode => IsModbusRtu && IsSlaveMode;

    partial void OnProtocolChanged(ProtocolType value)
    {
        OnPropertyChanged(nameof(IsModbusMasterMode));
        OnPropertyChanged(nameof(IsModbusSlaveMode));
    }

    partial void OnRoleChanged(ModbusRole value)
    {
        OnPropertyChanged(nameof(IsModbusMasterMode));
        OnPropertyChanged(nameof(IsModbusSlaveMode));
    }

    [ObservableProperty] private string _comPort = "COM1";
    [ObservableProperty] private int _baudRate = 9600;
    [ObservableProperty] private ModbusParity _parity = ModbusParity.None;
    [ObservableProperty] private int _dataBits = 8;
    [ObservableProperty] private int _stopBits = 1;
    [ObservableProperty] private int _stationAddress = 1;
    [ObservableProperty] private int _mySlaveId = 2;

    [ObservableProperty] private string _ipAddress = "192.168.0.10";
    [ObservableProperty] private int _port = 5000;
    [ObservableProperty] private McFrameFormat _mcFrameFormat = McFrameFormat.Binary;
    public IReadOnlyList<McFrameFormat> AvailableMcFrameFormats { get; } = Enum.GetValues<McFrameFormat>();

    [ObservableProperty] private int _pollingIntervalMs = 500;
    [ObservableProperty] private int _timeoutMs = 1000;
    [ObservableProperty] private int _retryCount = 3;

    [ObservableProperty] private int _inputBlockStartAddress = 1000;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputRegisterCountWarning))]
    private int _inputBlockRegisterCount = 20;

    [ObservableProperty] private int _outputBlockStartAddress = 1100;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputRegisterCountWarning))]
    private int _outputBlockRegisterCount = 20;

    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private ConnectionState _lastTestState = ConnectionState.Disconnected;

    /// <summary>1 dòng thông báo duy nhất dùng chung cho cả 3 nguồn (kết quả Test Connection, lỗi validate khi
    /// Lưu, xác nhận Lưu thành công) — cố ý KHÔNG dùng 3 property/TextBlock riêng như trước, vì chúng có thể
    /// cùng hiển thị đồng thời (VD Test Connection xong rồi Lưu thành công) khiến panel cao thêm, tràn thành
    /// thanh cuộn dọc không cần thiết. Mỗi hành động (Save/TestConnection) luôn ghi đè đúng 1 dòng này.</summary>
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _statusMessageIsError;

    public IReadOnlyList<ModbusRole> AvailableRoles { get; } = Enum.GetValues<ModbusRole>();
    public IReadOnlyList<string> AvailablePorts { get; private set; } = SerialPort.GetPortNames();
    public IReadOnlyList<int> AvailableBaudRates { get; } = [4800, 9600, 19200, 38400, 57600, 115200];
    public IReadOnlyList<ModbusParity> AvailableParities { get; } = Enum.GetValues<ModbusParity>();
    public IReadOnlyList<int> AvailableDataBits { get; } = [7, 8];
    public IReadOnlyList<int> AvailableStopBits { get; } = [1, 2];

    public string? InputRegisterCountWarning => BuildRegisterCountWarning(InputBlockRegisterCount);
    public string? OutputRegisterCountWarning => BuildRegisterCountWarning(OutputBlockRegisterCount);

    private static string? BuildRegisterCountWarning(int count) =>
        count > MaxRegistersPerModbusRequest
            ? string.Format(Translation.Instance["Setup_RegisterCountWarning"], count, MaxRegistersPerModbusRequest, (int)Math.Ceiling(count / (double)MaxRegistersPerModbusRequest))
            : null;

    /// <summary>Cột 1 và cột 2 của lưới "Giám sát DATA" — luôn đúng 12 phần tử/cột, tạo 1 lần trong constructor.</summary>
    public List<RegisterWatchRowViewModel> RegisterWatchColumn1 { get; } = [];
    public List<RegisterWatchRowViewModel> RegisterWatchColumn2 { get; } = [];

    private IEnumerable<RegisterWatchRowViewModel> AllRegisterWatchRows => RegisterWatchColumn1.Concat(RegisterWatchColumn2);

    public IReadOnlyList<RegisterDataType> AvailableDataTypes { get; } = Enum.GetValues<RegisterDataType>();
    public IReadOnlyList<RegisterDisplayFormat> AvailableDisplayFormats { get; } = Enum.GetValues<RegisterDisplayFormat>();

    public SetupTabViewModel(IConnectionSettingsStore settingsStore, IRegisterWatchStore registerWatchStore, PlcRegisterImage registerTable,
        PlcPollingService pollingService, IAuditLogService auditLog, IAuthenticationService auth, Dispatcher dispatcher)
    {
        _settingsStore = settingsStore;
        _registerWatchStore = registerWatchStore;
        _registerTable = registerTable;
        _pollingService = pollingService;
        _auditLog = auditLog;
        _auth = auth;

        for (var i = 0; i < RegisterWatchRowsPerColumn; i++)
        {
            var row = new RegisterWatchRowViewModel();
            row.ValueEditedByUser += OnRowValueEdited;
            RegisterWatchColumn1.Add(row);
        }
        for (var i = 0; i < RegisterWatchRowsPerColumn; i++)
        {
            var row = new RegisterWatchRowViewModel();
            row.ValueEditedByUser += OnRowValueEdited;
            RegisterWatchColumn2.Add(row);
        }

        _watchRefreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(500) };
        _watchRefreshTimer.Tick += (_, _) => RefreshWatchValues();
        _watchRefreshTimer.Start();
    }

    /// <summary>Đọc lại PlcRegisterImage (chia sẻ với Master/Slave) mỗi tick — không tự đọc driver, chỉ hiển thị giá trị mới nhất PlcPollingService/ModbusSlaveService đã cập nhật.</summary>
    private void RefreshWatchValues()
    {
        foreach (var row in AllRegisterWatchRows)
        {
            row.SetDisplayFromPoll(FormatRegisterValue(row));
        }
    }

    private string FormatRegisterValue(RegisterWatchRowViewModel row)
    {
        if (!ModbusWordAddress.TryParse(row.Address, out var addr) || addr.BitIndex is not null)
            return string.Empty;
        if (!_registerTable.TryGetWord(addr.WordAddress, out var low))
            return string.Empty;

        var needsSecondWord = RequiresSecondWord(row.DataType);
        uint rawBits = low;
        if (needsSecondWord)
        {
            if (!_registerTable.TryGetWord(addr.WordAddress + 1, out var high))
                return string.Empty;
            rawBits = ((uint)high << 16) | low; // word thấp trước — giả định, xem CLAUDE.md mục 13
        }

        return FormatBits(rawBits, needsSecondWord ? 32 : 16, row.DataType, row.DisplayFormat);
    }

    /// <summary>
    /// Diễn giải + hiển thị bit pattern gốc theo Định dạng đã chọn. Bin/Hex/Dec/Decimal hiển thị đúng giá
    /// trị số (signed/unsigned) mà Kiểu dữ liệu quy định; Float/Char bit-cast lại cùng bit pattern đó —
    /// hữu ích để "xem thử" 1 thanh ghi dưới nhiều góc nhìn khi chưa rõ PLC lưu kiểu gì.
    /// </summary>
    private static string FormatBits(uint rawBits, int bitWidth, RegisterDataType dataType, RegisterDisplayFormat format)
    {
        switch (format)
        {
            case RegisterDisplayFormat.Bin:
                return Convert.ToString(rawBits, 2).PadLeft(bitWidth, '0');
            case RegisterDisplayFormat.Hex:
                return bitWidth == 32 ? rawBits.ToString("X8") : rawBits.ToString("X4");
            case RegisterDisplayFormat.Float:
                return bitWidth == 32
                    ? BitConverter.UInt32BitsToSingle(rawBits).ToString("0.####", CultureInfo.InvariantCulture)
                    : ((float)BitConverter.UInt16BitsToHalf((ushort)rawBits)).ToString("0.####", CultureInfo.InvariantCulture);
            case RegisterDisplayFormat.Char:
                return DecodeChars(rawBits, bitWidth);
            case RegisterDisplayFormat.Decimal when dataType == RegisterDataType.Float32:
            case RegisterDisplayFormat.Dec when dataType == RegisterDataType.Float32:
                return BitConverter.UInt32BitsToSingle(rawBits).ToString("0.####", CultureInfo.InvariantCulture);
            case RegisterDisplayFormat.Dec:
                return GetNumericValue(rawBits, bitWidth, dataType).ToString(CultureInfo.InvariantCulture);
            case RegisterDisplayFormat.Decimal:
                return (GetNumericValue(rawBits, bitWidth, dataType) / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
            default:
                return string.Empty;
        }
    }

    private static long GetNumericValue(uint rawBits, int bitWidth, RegisterDataType dataType)
    {
        var signed = dataType is RegisterDataType.WordSigned or RegisterDataType.DwordSigned;
        if (!signed) return rawBits;
        return bitWidth == 32 ? (int)rawBits : (short)rawBits;
    }

    private static string DecodeChars(uint rawBits, int bitWidth)
    {
        if (bitWidth == 32)
        {
            Span<byte> bytes = [(byte)(rawBits >> 24), (byte)(rawBits >> 16), (byte)(rawBits >> 8), (byte)rawBits];
            return string.Concat(bytes.ToArray().Select(ToPrintableChar));
        }
        var high = (byte)(rawBits >> 8);
        var low = (byte)rawBits;
        return string.Concat(ToPrintableChar(high), ToPrintableChar(low));
    }

    private static char ToPrintableChar(byte b) => b is >= 32 and < 127 ? (char)b : '.';

    private static uint EncodeChars(string text, int bitWidth)
    {
        var maxChars = bitWidth == 32 ? 4 : 2;
        var padded = text.Length >= maxChars ? text[..maxChars] : text.PadRight(maxChars, '\0');
        uint result = 0;
        foreach (var c in padded)
            result = (result << 8) | (byte)c;
        return result;
    }

    /// <summary>Kiểm tra địa chỉ có nằm trong D0-D2000 hay không — rộng hơn Input/Output Block Start Address
    /// (chỉ D200-D2000), vì "Giám sát DATA" cho phép theo dõi/ghi cả dải D0-D99/D100-D199 cố định. Địa chỉ ngoài
    /// Input/Output Block đang polling vẫn ghi/hiển thị được nhưng không tự làm mới từ PLC mỗi chu kỳ — riêng
    /// D0-D99 vẫn được polling thật mỗi tick nên luôn hiển thị giá trị PLC thời gian thực.</summary>
    private string? ValidateWatchRow(RegisterWatchRowViewModel row)
    {
        if (string.IsNullOrWhiteSpace(row.Address)) return null; // ô trống, bỏ qua khi lưu
        if (!ModbusWordAddress.TryParse(row.Address, out var addr) || addr.BitIndex is not null)
            return Translation.Instance["Setup_RegisterWatchInvalidAddress"];

        var wordsNeeded = RequiresSecondWord(row.DataType) ? 2 : 1;
        for (var i = 0; i < wordsNeeded; i++)
        {
            if (!IsWithinAppOwnedRange(addr.WordAddress + i))
                return Translation.Instance["Setup_RegisterWatchOutOfRange"];
        }
        return null;
    }

    private const int MinRegisterWatchAddress = 0;

    private bool IsWithinAppOwnedRange(int wordAddress) =>
        wordAddress >= MinRegisterWatchAddress && wordAddress <= MaxConfigurableBlockAddress;

    public async Task LoadAsync()
    {
        var s = await _settingsStore.LoadAsync();
        Protocol = s.Protocol;
        Role = s.Role;
        ComPort = s.ComPort;
        BaudRate = s.BaudRate;
        Parity = s.Parity;
        DataBits = s.DataBits;
        StopBits = s.StopBits;
        StationAddress = s.StationAddress;
        MySlaveId = s.MySlaveId;
        IpAddress = s.IpAddress;
        Port = s.Port;
        McFrameFormat = s.McFrameFormat;
        PollingIntervalMs = s.PollingIntervalMs;
        TimeoutMs = s.TimeoutMs;
        RetryCount = s.RetryCount;
        InputBlockStartAddress = s.InputBlock.StartAddress;
        InputBlockRegisterCount = s.InputBlock.RegisterCount;
        OutputBlockStartAddress = s.OutputBlock.StartAddress;
        OutputBlockRegisterCount = s.OutputBlock.RegisterCount;

        ApplyRegisterTableRanges();

        var watchProfile = await _registerWatchStore.LoadAsync();
        var allRows = AllRegisterWatchRows.ToList();
        foreach (var def in watchProfile.Rows)
        {
            if (def.Order >= 0 && def.Order < allRows.Count)
                allRows[def.Order].ApplyDefinition(def);
        }
    }

    /// <summary>
    /// Nạp lại phạm vi địa chỉ hợp lệ cho PlcRegisterImage — đăng ký nguyên khối D200-D2000 (không chỉ đúng
    /// cửa sổ InputBlock/OutputBlock hiện hành), để "Giám sát DATA" có thể đọc/ghi bất kỳ địa chỉ nào trong
    /// khoảng app-owned này, không riêng phần đang được polling theo chu kỳ. Gọi lúc khởi động (LoadAsync) và
    /// mỗi khi lưu cấu hình mới (SaveAsync).
    /// </summary>
    private void ApplyRegisterTableRanges()
    {
        _registerTable.ConfigureAllowedRanges(
        [
            (MinConfigurableBlockAddress, MaxConfigurableBlockAddress - MinConfigurableBlockAddress + 1),
        ]);
    }

    public void RefreshPermissions(bool isAdmin) => IsAdmin = isAdmin;

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts = SerialPort.GetPortNames();
        OnPropertyChanged(nameof(AvailablePorts));
    }

    /// <summary>D0-D99 luôn chỉ-đọc và D100-D199 luôn chỉ-ghi theo quy tắc cứng của <see cref="PlcRegisterImage"/>
    /// (Role=Master) — Input/Output Block cấu hình ở đây phải nằm trong khoảng D200-D2000 để không chồng lấn 2 dải
    /// cố định đó, tránh cấu hình gây hiểu nhầm (VD đặt Output Block start=50 khiến vòng ghi mỗi tick cố ghi đè lên
    /// dải chỉ-đọc — bị chặn ở tầng PlcRegisterImage nhưng vẫn lãng phí request và gây khó hiểu khi debug).</summary>
    private const int MinConfigurableBlockAddress = 200;
    private const int MaxConfigurableBlockAddress = 2000;

    /// <summary>Chặn Lưu/Test Connection chạy chồng lấn — cả 2 đều có thể mở/đóng lại cổng COM thật
    /// (RestartConnectionAsync, hoặc driver tạm trong TestConnectionAsync). [RelayCommand] mặc định CHO PHÉP
    /// chạy chồng lấn (AllowConcurrentExecutions=true) — bấm 2 lần liên tiếp trong lúc thao tác trước còn đang
    /// chờ đóng cổng COM cũ (chậm hơn hẳn khi đang timeout liên tục do sai Parity với PLC) sẽ khiến 2 lệnh cùng
    /// cố mở COM4 → UnauthorizedAccessException "Access denied", dù giá trị Parity không hề sai. Bug thật đã gặp.</summary>
    private bool _connectionOperationInProgress;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!IsAdmin || _connectionOperationInProgress) return;
        _connectionOperationInProgress = true;
        try
        {
            await SaveCoreAsync();
        }
        finally
        {
            _connectionOperationInProgress = false;
        }
    }

    private async Task SaveCoreAsync()
    {
        if (InputBlockStartAddress < MinConfigurableBlockAddress || InputBlockStartAddress > MaxConfigurableBlockAddress ||
            OutputBlockStartAddress < MinConfigurableBlockAddress || OutputBlockStartAddress > MaxConfigurableBlockAddress)
        {
            StatusMessageIsError = true;
            StatusMessage = string.Format(Translation.Instance["Setup_BlockStartAddressOutOfRange"], MinConfigurableBlockAddress, MaxConfigurableBlockAddress);
            return;
        }

        foreach (var row in AllRegisterWatchRows)
        {
            row.ValidationMessage = ValidateWatchRow(row);
        }
        // .Distinct() — nhiều dòng "Giám sát DATA" thường trùng cùng 1 lý do lỗi (VD hàng loạt địa chỉ cùng ra
        // ngoài phạm vi sau khi đổi Input/Output Block), ghép thẳng không lọc trùng sẽ tạo 1 dòng thông báo lặp
        // lại rất dài, khó đọc.
        var errors = AllRegisterWatchRows.Select(r => r.ValidationMessage).Where(e => e is not null).Distinct().ToList();
        if (errors.Count > 0)
        {
            StatusMessageIsError = true;
            StatusMessage = string.Join(" ", errors);
            return;
        }

        await _settingsStore.SaveAsync(new ConnectionSettings
        {
            Protocol = Protocol,
            Role = Role,
            ComPort = ComPort,
            BaudRate = BaudRate,
            Parity = Parity,
            DataBits = DataBits,
            StopBits = StopBits,
            StationAddress = StationAddress,
            MySlaveId = MySlaveId,
            IpAddress = IpAddress,
            Port = Port,
            McFrameFormat = McFrameFormat,
            PollingIntervalMs = PollingIntervalMs,
            TimeoutMs = TimeoutMs,
            RetryCount = RetryCount,
            InputBlock = new ModbusRegisterBlock { StartAddress = InputBlockStartAddress, RegisterCount = InputBlockRegisterCount },
            OutputBlock = new ModbusRegisterBlock { StartAddress = OutputBlockStartAddress, RegisterCount = OutputBlockRegisterCount },
        });

        ApplyRegisterTableRanges();
        _pollingService.SetInputBlockRange(InputBlockStartAddress, InputBlockRegisterCount);
        _pollingService.SetOutputBlockRange(OutputBlockStartAddress, OutputBlockRegisterCount);
        _pollingService.SetPollingIntervalMs(PollingIntervalMs);

        // Áp dụng ComPort/Baud/Slave ID/.../v.v. NGAY, không cần khởi động lại app — chỉ khi phiên app hiện
        // tại thực sự đang chạy Master (IsActiveMaster, không phải giá trị Role đang gõ dở trên form này).
        // Đổi Role (Master/Slave) vẫn luôn cần khởi động lại (xem Setup_RoleChangeRestartHint).
        if (_pollingService.IsActiveMaster)
        {
            await _pollingService.RestartConnectionAsync();
        }

        var watchProfile = new RegisterWatchProfile
        {
            Rows = AllRegisterWatchRows
                .Select((r, i) => (Row: r, Index: i))
                .Where(x => !string.IsNullOrWhiteSpace(x.Row.Address))
                .Select(x => x.Row.ToDefinition(x.Index))
                .ToList(),
        };
        await _registerWatchStore.SaveAsync(watchProfile);

        StatusMessageIsError = false;
        StatusMessage = string.Format(Translation.Instance["Setup_SaveSuccess"], DateTime.Now.ToString("HH:mm:ss"));
    }

    /// <summary>
    /// Test kết nối THẬT bằng đúng giá trị ĐANG HIỂN THỊ trên form (kể cả chưa bấm "Lưu cấu hình") — tự tạo
    /// 1 driver tạm thời riêng theo <see cref="Protocol"/>, dùng xong hủy ngay. Trước đây gọi thẳng
    /// <see cref="PlcPollingService.TestConnectionAsync"/> — dùng driver ĐANG CHẠY (theo cấu hình đã LƯU lần
    /// gần nhất), nên nếu Admin vừa đổi Protocol/IP/Port trên form nhưng CHƯA bấm Lưu, nút Test Connection vẫn
    /// âm thầm test theo cấu hình CŨ — bug thật đã gặp: đổi sang MC Protocol/SLMP rồi bấm Test Connection vẫn
    /// báo lỗi cổng COM Modbus cũ (VD "Could not find file 'COM99'"), gây hiểu nhầm nghiêm trọng.
    /// <para>
    /// BUG THẬT KHÁC đã gặp: <see cref="ModbusRtu.ModbusRtuDriver"/> (driver Master đang chạy polling sống)
    /// giữ cổng COM mở LIÊN TỤC suốt phiên 1 khi đã connect — driver tạm thời ở đây không bao giờ mở được cùng
    /// 1 cổng COM cùng lúc (UnauthorizedAccessException "Access denied"), BẤT KỂ giá trị đang test khớp cấu
    /// hình đã lưu hay không. Đã sửa: nếu đang Role=Master sống (<see cref="PlcPollingService.IsActiveMaster"/>),
    /// tạm dừng hẳn kết nối sống (<see cref="PlcPollingService.PauseAsync"/>) TRƯỚC khi test, rồi khôi phục lại
    /// đúng cấu hình đã lưu (<see cref="PlcPollingService.RestartConnectionAsync"/>) SAU khi test xong — dù test
    /// thành công hay lỗi, đảm bảo bằng try/finally.
    /// </para>
    /// </summary>
    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (_connectionOperationInProgress) return;
        _connectionOperationInProgress = true;
        var pausedLiveMaster = _pollingService.IsActiveMaster;
        try
        {
            if (pausedLiveMaster) await _pollingService.PauseAsync();
            await TestConnectionCoreAsync();
        }
        finally
        {
            // Nested try/finally: nếu RestartConnectionAsync() ném (VD cổng COM vừa biến mất thật), cờ
            // _connectionOperationInProgress vẫn PHẢI được nhả — nếu không, mọi lần bấm Lưu/Test Connection
            // sau đó bị chặn im lặng suốt phiên. Exception (nếu có) vẫn bay lên lưới an toàn Dispatcher.
            try
            {
                if (pausedLiveMaster) await _pollingService.RestartConnectionAsync();
            }
            finally
            {
                _connectionOperationInProgress = false;
            }
        }
    }

    private async Task TestConnectionCoreAsync()
    {
        LastTestState = ConnectionState.Connecting;

        IPlcCommunicationDriver driver = Protocol switch
        {
            ProtocolType.ModbusRtu => new EolTester.Communication.ModbusRtu.ModbusRtuDriver(),
            ProtocolType.McProtocol => new EolTester.Communication.Mc.McProtocolDriver(),
            ProtocolType.Slmp => new EolTester.Communication.Slmp.SlmpDriver(),
            _ => throw new NotSupportedException($"Giao thức {Protocol} chưa được hỗ trợ ở vai trò Master."),
        };

        int value;
        try
        {
            switch (Protocol)
            {
                case ProtocolType.ModbusRtu:
                    ((EolTester.Communication.ModbusRtu.ModbusRtuDriver)driver).Configure(
                        ComPort, BaudRate, Parity.ToSerialPortParity(), DataBits, StopBits.ToSerialPortStopBits(), (byte)StationAddress, TimeoutMs, RetryCount);
                    break;
                case ProtocolType.McProtocol:
                    ((EolTester.Communication.Mc.McProtocolDriver)driver).Configure(IpAddress, Port, TimeoutMs, RetryCount, McFrameFormat.ToMcFrameFormatKind());
                    break;
                case ProtocolType.Slmp:
                    ((EolTester.Communication.Slmp.SlmpDriver)driver).Configure(IpAddress, Port, TimeoutMs, RetryCount);
                    break;
            }

            value = await driver.ReadRegisterAsync(new IoAddress(InputBlockStartAddress.ToString()), CancellationToken.None);
        }
        catch (Exception ex)
        {
            LastTestState = ConnectionState.Error;
            StatusMessageIsError = true;
            StatusMessage = string.Format(Translation.Instance["Setup_TestResultError"], ex.Message);
            return;
        }
        finally
        {
            await driver.DisposeAsync();
        }

        LastTestState = ConnectionState.Connected;
        StatusMessageIsError = false;
        var endpointDescription = IsModbusRtu ? $"{ComPort}@{BaudRate}" : $"{IpAddress}:{Port}";
        StatusMessage = string.Format(Translation.Instance["Setup_TestResultReal"], Protocol, endpointDescription, DateTime.Now.ToString("HH:mm:ss"), value);
    }

    private void OnRowValueEdited(object? sender, EventArgs e)
    {
        if (sender is RegisterWatchRowViewModel row) _ = CommitRegisterWriteAsync(row);
    }

    /// <summary>
    /// Ghi giá trị Admin vừa gõ (Enter/rời ô cột "Giá trị") xuống thanh ghi bất kỳ đâu trong D0-D2000, kể cả
    /// D0-D99 (chỉ-đọc theo quy tắc cứng của PlcRegisterImage) — nếu đang polling thật, giá trị vừa ghi sẽ
    /// bị lần đọc thật tiếp theo (~500ms) ghi đè lại, đúng ý định dùng để test/theo dõi tạm thời, không phải
    /// ghi đè vĩnh viễn. Ở Role=Master: đẩy ra PLC thật qua driver. Ở Role=Slave: chỉ cập nhật
    /// PlcRegisterImage (PC không có kết nối Master chủ động) — đúng bằng giá trị PLC sẽ đọc được ở lần
    /// hỏi tiếp theo, vì ModbusSlaveService đọc trực tiếp từ cùng bảng này (xem mục 6 CLAUDE.md).
    /// </summary>
    private async Task CommitRegisterWriteAsync(RegisterWatchRowViewModel row)
    {
        if (!ModbusWordAddress.TryParse(row.Address, out var addr) || addr.BitIndex is not null)
        {
            row.ValidationMessage = Translation.Instance["Setup_RegisterWatchInvalidAddress"];
            row.SetDisplayFromPoll(FormatRegisterValue(row));
            return;
        }

        var needsSecondWord = RequiresSecondWord(row.DataType);
        if (!IsWithinAppOwnedRange(addr.WordAddress) || (needsSecondWord && !IsWithinAppOwnedRange(addr.WordAddress + 1)))
        {
            row.ValidationMessage = Translation.Instance["Setup_RegisterWatchOutOfRange"];
            row.SetDisplayFromPoll(FormatRegisterValue(row));
            return;
        }

        if (!TryParseWriteValue(row, needsSecondWord, out var low, out var high))
        {
            row.ValidationMessage = Translation.Instance["Setup_RegisterWatchInvalidWriteValue"];
            row.SetDisplayFromPoll(FormatRegisterValue(row));
            return;
        }

        // Ghi đè cache ngay lập tức bất kể phạm vi CanWriteAddress (kể cả D0-D99, kể cả mất kết nối PLC) —
        // Admin cần thấy giá trị mình vừa gõ ngay, không phụ thuộc việc ghi vật lý xuống PLC có thành công hay không.
        _registerTable.ForceSetWordForAdminOverride(addr.WordAddress, low);
        if (needsSecondWord) _registerTable.ForceSetWordForAdminOverride(addr.WordAddress + 1, high);

        if (IsMasterMode)
        {
            await _pollingService.WriteRawWordAsync(addr.WordAddress, low);
            if (needsSecondWord) await _pollingService.WriteRawWordAsync(addr.WordAddress + 1, high);
        }

        var userName = _auth.CurrentUser?.UserName ?? "?";
        await _auditLog.LogAsync(userName, $"Ghi giá trị thanh ghi {row.Address}", newValue: row.CurrentValueDisplay);
        row.ValidationMessage = null;
    }

    private static bool RequiresSecondWord(RegisterDataType type) =>
        type is RegisterDataType.DwordSigned or RegisterDataType.DwordUnsigned or RegisterDataType.Float32;

    /// <summary>Diễn giải chuỗi Admin vừa gõ theo đúng Định dạng của dòng thành 1-2 từ 16-bit (word thấp trước cho Dword/Float).</summary>
    private static bool TryParseWriteValue(RegisterWatchRowViewModel row, bool needsSecondWord, out ushort low, out ushort high)
    {
        low = 0;
        high = 0;
        var text = row.CurrentValueDisplay.Trim();
        if (text.Length == 0) return false;

        var bitWidth = needsSecondWord ? 32 : 16;

        try
        {
            uint rawBits;
            switch (row.DisplayFormat)
            {
                case RegisterDisplayFormat.Bin:
                    rawBits = Convert.ToUInt32(text, 2);
                    break;
                case RegisterDisplayFormat.Hex:
                    rawBits = Convert.ToUInt32(text, 16);
                    break;
                case RegisterDisplayFormat.Dec when row.DataType == RegisterDataType.Float32:
                case RegisterDisplayFormat.Decimal when row.DataType == RegisterDataType.Float32:
                    rawBits = BitConverter.SingleToUInt32Bits(float.Parse(text, CultureInfo.InvariantCulture));
                    break;
                case RegisterDisplayFormat.Dec:
                    var intValue = long.Parse(text, CultureInfo.InvariantCulture);
                    rawBits = bitWidth == 32 ? unchecked((uint)intValue) : unchecked((ushort)intValue);
                    break;
                case RegisterDisplayFormat.Decimal:
                    var scaled = decimal.Parse(text, CultureInfo.InvariantCulture);
                    var scaledInt = (long)Math.Round(scaled * 100m, MidpointRounding.AwayFromZero);
                    rawBits = bitWidth == 32 ? unchecked((uint)scaledInt) : unchecked((ushort)scaledInt);
                    break;
                case RegisterDisplayFormat.Float:
                    var floatValue = float.Parse(text, CultureInfo.InvariantCulture);
                    rawBits = bitWidth == 32
                        ? BitConverter.SingleToUInt32Bits(floatValue)
                        : BitConverter.HalfToUInt16Bits((Half)floatValue);
                    break;
                case RegisterDisplayFormat.Char:
                    rawBits = EncodeChars(text, bitWidth);
                    break;
                default:
                    return false;
            }

            low = (ushort)(rawBits & 0xFFFF);
            high = (ushort)(rawBits >> 16);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return false;
        }
    }
}
