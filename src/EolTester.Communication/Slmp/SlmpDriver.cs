using System.Net.Sockets;
using EolTester.Communication.Mc;
using EolTester.Core.Enums;

namespace EolTester.Communication.Slmp;

/// <summary>
/// Driver SLMP thật — PC làm Master, kết nối TCP tới PLC. SLMP là bản kế thừa của MC Protocol qua Ethernet,
/// chia sẻ cùng cấu trúc khung 3E (xem <see cref="Mc3EBinaryFrameCodec"/>) nhưng luôn Binary (không có lựa
/// chọn ASCII như MC Protocol) — vì vậy driver này KHÔNG dùng NModbus/SerialPort, tái dùng thẳng codec 3E
/// Binary. Tách file/namespace riêng khỏi <see cref="McProtocolDriver"/> theo đúng thiết kế đã chốt (driver
/// riêng cho từng giao thức, dù dùng chung codec byte-level) — xem CLAUDE.md.
/// </summary>
public sealed class SlmpDriver : IPlcCommunicationDriver
{
    private readonly IMc3EFrameCodec _codec = new Mc3EBinaryFrameCodec();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private ConnectionState _state = ConnectionState.Disconnected;

    private string _ipAddress = "192.168.0.10";
    private int _port = 5001;
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

    public void Configure(string ipAddress, int port, int timeoutMs, int retryCount)
    {
        _ipAddress = ipAddress;
        _port = port;
        _timeoutMs = timeoutMs;
        _retryCount = retryCount;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        State = ConnectionState.Connecting;
        CloseInternal();
        try
        {
            _client = new TcpClient();
            var connectTask = _client.ConnectAsync(_ipAddress, _port, ct).AsTask();
            var completed = await Task.WhenAny(connectTask, Task.Delay(_timeoutMs, ct));
            if (completed != connectTask)
            {
                // Cùng bug/nguyên tắc đã sửa ở McProtocolDriver — không dựa vào CancellationToken của chính
                // TcpClient.ConnectAsync, tự đua với Task.Delay rồi đóng socket để buộc hủy khi quá giờ.
                CloseInternal();
                throw new TimeoutException($"Kết nối TCP tới {_ipAddress}:{_port} quá thời gian chờ ({_timeoutMs}ms).");
            }
            await connectTask;
            _stream = _client.GetStream();
            _stream.ReadTimeout = _timeoutMs;
            _stream.WriteTimeout = _timeoutMs;
            // Cùng nguyên tắc ModbusRtuDriver/McProtocolDriver — chưa đặt Connected ở đây, chỉ xác nhận sau
            // lần đọc/ghi khung SLMP thành công đầu tiên.
        }
        catch
        {
            CloseInternal();
            State = ConnectionState.Error;
            throw;
        }
    }

    public Task DisconnectAsync()
    {
        CloseInternal();
        State = ConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    private void CloseInternal()
    {
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_stream is not null) return;
        await ConnectAsync(ct);
    }

    public async Task<bool> ReadDiscreteAsync(IoAddress address, CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
        try
        {
            var request = _codec.EncodeReadBitRequest(ParseDevice(address), 1);
            var response = await SendAndReceiveAsync(request, ct);
            var result = _codec.DecodeReadBitResponse(response, 1)[0];
            State = ConnectionState.Connected;
            return result;
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
            var request = _codec.EncodeWriteBitRequest(ParseDevice(address), [value]);
            var response = await SendAndReceiveAsync(request, ct);
            _codec.ValidateWriteResponse(response);
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
            var request = _codec.EncodeReadWordRequest(ParseDevice(address), 1);
            var response = await SendAndReceiveAsync(request, ct);
            var result = _codec.DecodeReadWordResponse(response, 1)[0];
            State = ConnectionState.Connected;
            return result;
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
            var request = _codec.EncodeWriteWordRequest(ParseDevice(address), [(ushort)value]);
            var response = await SendAndReceiveAsync(request, ct);
            _codec.ValidateWriteResponse(response);
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
            var request = _codec.EncodeReadWordRequest(ParseDevice(startAddress), count);
            var response = await SendAndReceiveAsync(request, ct);
            var result = _codec.DecodeReadWordResponse(response, count);
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
            var request = _codec.EncodeWriteWordRequest(ParseDevice(startAddress), data);
            var response = await SendAndReceiveAsync(request, ct);
            _codec.ValidateWriteResponse(response);
            State = ConnectionState.Connected;
        }
        catch
        {
            State = ConnectionState.Error;
            throw;
        }
    }

    private static int ParseDevice(IoAddress address) => int.Parse(address.Value);

    private async Task<byte[]> SendAndReceiveAsync(byte[] request, CancellationToken ct)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt <= _retryCount; attempt++)
        {
            try
            {
                await _stream!.WriteAsync(request, ct);

                var header = await ReadExactAsync(_codec.ResponseHeaderLength, ct);
                var dataLength = _codec.GetResponseDataLength(header);
                var rest = await ReadExactAsync(dataLength, ct);

                var full = new byte[header.Length + rest.Length];
                header.CopyTo(full, 0);
                rest.CopyTo(full, header.Length);
                return full;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
            }
        }

        throw lastError!;
    }

    private async Task<byte[]> ReadExactAsync(int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await _stream!.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (read == 0) throw new IOException("Kết nối TCP tới PLC đã đóng giữa chừng khi đang đọc phản hồi SLMP.");
            offset += read;
        }
        return buffer;
    }

    public ValueTask DisposeAsync()
    {
        CloseInternal();
        State = ConnectionState.Disconnected;
        return ValueTask.CompletedTask;
    }
}
