using System.IO.Ports;
using EolTester.Core.Enums;
using NModbus;
using NModbus.IO;

namespace EolTester.Communication.ModbusRtu;

/// <summary>
/// Driver Modbus RTU thật — PC làm Master, mở cổng COM thật và giao tiếp qua NModbus. Cài đặt duy nhất của
/// <see cref="IPlcCommunicationDriver"/> nói chuyện với phần cứng thật; ViewModel/View không bao giờ biết tới
/// lớp này, chỉ biết interface (xem CLAUDE.md mục 6). Đây là driver DUY NHẤT dùng khi Role=Master (đã bỏ
/// driver giả lập Mock — rủi ro vận hành nếu ai đó vô tình chọn nhầm ở máy thật, xem CLAUDE.md mục 9).
/// </summary>
public sealed class ModbusRtuDriver : IPlcCommunicationDriver
{
    private SerialPort? _serialPort;
    private IModbusMaster? _master;
    private ConnectionState _state = ConnectionState.Disconnected;

    private string _comPort = "COM1";
    private int _baudRate = 9600;
    private Parity _parity = Parity.None;
    private int _dataBits = 8;
    private StopBits _stopBits = StopBits.One;
    private byte _stationAddress = 1;
    private int _timeoutMs = 1000;
    private int _retryCount = 3;

    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<ConnectionState>? StateChanged;

    /// <summary>
    /// Nạp thông số cổng COM/giao thức — gọi trước <see cref="ConnectAsync"/>, do <c>PlcPollingService.StartAsync</c>
    /// thực hiện ngay sau khi tải <c>ConnectionSettings</c>. Gọi lại được nhiều lần (VD Admin đổi cấu hình rồi
    /// khởi động lại app) — chỉ cập nhật tham số trong bộ nhớ, không tự mở lại cổng COM.
    /// </summary>
    public void Configure(string comPort, int baudRate, Parity parity, int dataBits, StopBits stopBits, byte stationAddress, int timeoutMs, int retryCount)
    {
        _comPort = comPort;
        _baudRate = baudRate;
        _parity = parity;
        _dataBits = dataBits;
        _stopBits = stopBits;
        _stationAddress = stationAddress;
        _timeoutMs = timeoutMs;
        _retryCount = retryCount;
    }

    public Task ConnectAsync(CancellationToken ct)
    {
        State = ConnectionState.Connecting;
        CloseInternal();
        try
        {
            _serialPort = new SerialPort(_comPort, _baudRate, _parity, _dataBits, _stopBits)
            {
                ReadTimeout = _timeoutMs,
                WriteTimeout = _timeoutMs,
            };
            _serialPort.Open();
            var stream = new StreamAdapter(_serialPort.BaseStream, _serialPort.DiscardInBuffer)
            {
                ReadTimeout = _timeoutMs,
                WriteTimeout = _timeoutMs,
            };
            AttachStream(stream);
            // CỐ Ý không đặt State = Connected ở đây — mở cổng COM thành công chỉ chứng minh cổng vật lý sẵn
            // sàng, KHÔNG chứng minh PLC thật đang có mặt/phản hồi trên bus (VD cổng COM tồn tại nhưng chưa
            // cắm cáp/PLC tắt vẫn mở cổng bình thường). "Connected" chỉ được xác nhận đúng nghĩa sau lần đọc/ghi
            // Modbus đầu tiên thành công (xem Read/WriteRegisterAsync/DiscreteAsync) — giữ đúng tinh thần CLAUDE.md
            // mục 3: chỉ báo trạng thái phải phản ánh thực tế, không chỉ "đã thử kết nối".
        }
        catch
        {
            CloseInternal();
            State = ConnectionState.Error;
            throw;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gắn thẳng 1 <see cref="IStreamResource"/> có sẵn — tách khỏi <see cref="ConnectAsync"/> (vốn luôn mở
    /// <see cref="SerialPort"/> thật) để integration test có thể truyền vào 1 stream giả lập (TCP loopback),
    /// cùng pattern <c>ModbusSlaveService.StartWithStreamAsync</c>. Không tự đặt <see cref="State"/> = Connected
    /// — chỉ tạo xong master transport, chưa xác nhận PLC/slave đầu kia thực sự phản hồi.
    /// </summary>
    internal void AttachStream(IStreamResource stream)
    {
        var factory = new ModbusFactory();
        var master = factory.CreateRtuMaster(stream);
        master.Transport.Retries = _retryCount;
        master.Transport.ReadTimeout = _timeoutMs;
        master.Transport.WriteTimeout = _timeoutMs;
        _master = master;
    }

    public Task DisconnectAsync()
    {
        CloseInternal();
        State = ConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    private void CloseInternal()
    {
        _master = null;
        _serialPort?.Dispose();
        _serialPort = null;
    }

    /// <summary>Mở lại cổng COM nếu chưa có master transport nào (chưa từng kết nối, hoặc vừa Disconnect/Dispose).
    /// Không dựa vào <see cref="State"/> để quyết định — 1 request đọc/ghi lỗi giữa phiên (VD PLC tạm mất tín
    /// hiệu) chỉ chuyển <see cref="State"/> sang <c>Error</c>, KHÔNG đóng cổng COM đang mở; NModbus tự retry theo
    /// <c>Transport.Retries</c> trong mỗi request, và tick polling ~500ms kế tiếp tự nhiên đóng vai trò thử lại —
    /// không cần mở lại cổng COM mỗi lần lỗi.</summary>
    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_master is not null) return;
        await ConnectAsync(ct);
    }

    public async Task<bool> ReadDiscreteAsync(IoAddress address, CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
        try
        {
            var result = await _master!.ReadCoilsAsync(_stationAddress, ParseAddress(address), 1);
            State = ConnectionState.Connected;
            return result[0];
        }
        catch
        {
            State = ConnectionState.Error;
            throw;
        }
    }

    public async Task WriteDiscreteAsync(IoAddress address, bool value, CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
        try
        {
            await _master!.WriteSingleCoilAsync(_stationAddress, ParseAddress(address), value);
            State = ConnectionState.Connected;
        }
        catch
        {
            State = ConnectionState.Error;
            throw;
        }
    }

    public async Task<int> ReadRegisterAsync(IoAddress address, CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
        try
        {
            var result = await _master!.ReadHoldingRegistersAsync(_stationAddress, ParseAddress(address), 1);
            State = ConnectionState.Connected;
            return result[0];
        }
        catch
        {
            State = ConnectionState.Error;
            throw;
        }
    }

    public async Task WriteRegisterAsync(IoAddress address, int value, CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
        try
        {
            await _master!.WriteSingleRegisterAsync(_stationAddress, ParseAddress(address), (ushort)value);
            State = ConnectionState.Connected;
        }
        catch
        {
            State = ConnectionState.Error;
            throw;
        }
    }

    public async Task<int[]> ReadRegistersAsync(IoAddress startAddress, int count, CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
        try
        {
            var result = await _master!.ReadHoldingRegistersAsync(_stationAddress, ParseAddress(startAddress), (ushort)count);
            State = ConnectionState.Connected;
            return Array.ConvertAll(result, v => (int)v);
        }
        catch
        {
            State = ConnectionState.Error;
            throw;
        }
    }

    public async Task WriteRegistersAsync(IoAddress startAddress, IReadOnlyList<int> values, CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
        try
        {
            var data = new ushort[values.Count];
            for (var i = 0; i < values.Count; i++) data[i] = (ushort)values[i];
            await _master!.WriteMultipleRegistersAsync(_stationAddress, ParseAddress(startAddress), data);
            State = ConnectionState.Connected;
        }
        catch
        {
            State = ConnectionState.Error;
            throw;
        }
    }

    private static ushort ParseAddress(IoAddress address) => ushort.Parse(address.Value);

    public ValueTask DisposeAsync()
    {
        CloseInternal();
        State = ConnectionState.Disconnected;
        return ValueTask.CompletedTask;
    }
}
