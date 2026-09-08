using System.IO.Ports;
using EolTester.Core.Enums;
using NModbus;
using NModbus.IO;

namespace EolTester.Communication;

/// <summary>
/// PC làm Modbus RTU Slave — bị động lắng nghe, PLC (Master) chủ động gửi lệnh đọc/ghi tới PC theo
/// <c>MySlaveId</c>. Chạy song song loại trừ với <see cref="EolTester.App.Services.PlcPollingService"/>
/// (Master) — chỉ 1 trong 2 được khởi động tại 1 thời điểm theo <c>ConnectionSettings.Role</c>, vì RS-485/
/// Modbus RTU chỉ cho phép 1 master trên bus. Dùng chung <see cref="PlcRegisterImage"/> làm nguồn dữ liệu
/// với Master — đọc/ghi qua NModbus phản ánh thẳng vào bảng, không cần đồng bộ định kỳ (xem
/// <see cref="PlcRegisterImageDataStore"/>). Cài đặt của <see cref="IPlcSlaveService"/> — xem CLAUDE.md
/// mục "Kiến trúc lõi: Bảng thanh ghi trung gian".
/// </summary>
public sealed class ModbusSlaveService : IPlcSlaveService
{
    private readonly PlcRegisterImage _registerTable;
    private SerialPort? _serialPort;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private ConnectionState _state = ConnectionState.Disconnected;

    private string _comPort = "COM1";
    private int _baudRate = 9600;
    private Parity _parity = Parity.None;
    private int _dataBits = 8;
    private StopBits _stopBits = StopBits.One;
    private byte _mySlaveId = 2;

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

    public ModbusSlaveService(PlcRegisterImage registerTable)
    {
        _registerTable = registerTable;
    }

    /// <summary>Nạp thông số cổng COM/Slave ID — gọi trước <see cref="StartAsync(CancellationToken)"/>, cùng
    /// pattern <c>ModbusRtuDriver.Configure</c>.</summary>
    public void Configure(string comPort, int baudRate, Parity parity, int dataBits, StopBits stopBits, byte mySlaveId)
    {
        _comPort = comPort;
        _baudRate = baudRate;
        _parity = parity;
        _dataBits = dataBits;
        _stopBits = stopBits;
        _mySlaveId = mySlaveId;
    }

    /// <summary>Cài đặt <see cref="IPlcSlaveService.StartAsync"/> — dùng thông số đã nạp qua <see cref="Configure"/>.</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        State = ConnectionState.Connecting;
        try
        {
            await StartAsync(_comPort, _baudRate, _parity, _dataBits, _stopBits, _mySlaveId, ct);
            State = ConnectionState.Connected;
        }
        catch
        {
            State = ConnectionState.Error;
            throw;
        }
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        _serialPort?.Dispose();
        _serialPort = null;
        State = ConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public Task StartAsync(string comPort, int baudRate, Parity parity, int dataBits, StopBits stopBits, byte mySlaveId, CancellationToken ct = default)
    {
        _serialPort = new SerialPort(comPort, baudRate, parity, dataBits, stopBits);
        _serialPort.Open();
        var stream = new StreamAdapter(_serialPort.BaseStream, _serialPort.DiscardInBuffer);
        return StartWithStreamAsync(stream, mySlaveId, ct);
    }

    /// <summary>
    /// Lõi khởi động, nhận thẳng <see cref="IStreamResource"/> — tách riêng khỏi <see cref="StartAsync"/>
    /// (vốn luôn mở <see cref="SerialPort"/> thật) để unit/integration test có thể truyền vào 1 stream
    /// giả lập (VD TCP loopback) mà không cần cổng COM thật hay driver COM ảo.
    /// </summary>
    internal Task StartWithStreamAsync(IStreamResource stream, byte mySlaveId, CancellationToken ct = default)
    {
        var factory = new ModbusFactory();
        var dataStore = new PlcRegisterImageDataStore(_registerTable);
        var slave = factory.CreateSlave(mySlaveId, dataStore);
        var network = factory.CreateRtuSlaveNetwork(stream);
        network.AddSlave(slave);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listenTask = Task.Run(() => network.ListenAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_listenTask is not null)
        {
            try { await _listenTask; } catch { /* ignore — hủy do dừng service */ }
        }
        _serialPort?.Dispose();
    }
}
