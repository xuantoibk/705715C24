using System.Net.Sockets;
using EolTester.Core.Enums;

namespace EolTester.Communication.Mc;

public enum McFrameFormatKind
{
    Binary,
    Ascii
}

/// <summary>
/// Driver MC Protocol thật — PC làm Master, kết nối TCP tới PLC (khung 3E, Binary hoặc ASCII tùy
/// <see cref="McFrameFormatKind"/>). Cùng vai trò với <see cref="EolTester.Communication.ModbusRtu.ModbusRtuDriver"/>
/// nhưng qua Ethernet thay vì cổng COM. Chỉ thao tác thiết bị "D" (word register) cho Read/WriteRegisterAsync —
/// khớp quy ước Dxxxx dùng xuyên suốt dự án; Read/WriteDiscreteAsync dùng thiết bị "M" theo quy ước ĐƠN GIẢN HÓA
/// (xem <see cref="IMc3EFrameCodec"/>). GIẢ ĐỊNH cấu trúc khung 3E theo tài liệu Mitsubishi phổ biến — cần đối
/// chiếu khi có PLC MC thật (xem CLAUDE.md mục 13), giống Modbus RTU trước khi có PLC thật xác nhận D1000-D1002.
/// </summary>
public sealed class McProtocolDriver : IPlcCommunicationDriver
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private ConnectionState _state = ConnectionState.Disconnected;

    private string _ipAddress = "192.168.0.10";
    private int _port = 5000;
    private int _timeoutMs = 1000;
    private int _retryCount = 3;
    private IMc3EFrameCodec _codec = new Mc3EBinaryFrameCodec();

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

    /// <summary>Nạp thông số IP/Port/định dạng khung — gọi trước <see cref="ConnectAsync"/>, cùng pattern
    /// <c>ModbusRtuDriver.Configure</c>.</summary>
    public void Configure(string ipAddress, int port, int timeoutMs, int retryCount, McFrameFormatKind frameFormat)
    {
        _ipAddress = ipAddress;
        _port = port;
        _timeoutMs = timeoutMs;
        _retryCount = retryCount;
        _codec = frameFormat == McFrameFormatKind.Ascii ? new Mc3EAsciiFrameCodec() : new Mc3EBinaryFrameCodec();
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
                // KHÔNG dựa vào CancellationToken của chính TcpClient.ConnectAsync để tự hủy khi quá giờ —
                // bug thật đã gặp: với 1 IP có route nhưng không có host phản hồi (VD PLC tắt/sai IP), lần kết
                // nối treo lâu hơn NHIỀU so với TimeoutMs đã cấu hình dù truyền CancellationToken vào, khiến cả
                // vòng lặp polling đứng hình. Đua với Task.Delay rồi CHỦ ĐỘNG đóng socket để buộc hủy connect
                // đang treo, đảm bảo luôn có giới hạn thời gian chờ thật sự.
                CloseInternal();
                throw new TimeoutException($"Kết nối TCP tới {_ipAddress}:{_port} quá thời gian chờ ({_timeoutMs}ms).");
            }
            await connectTask; // ném lại lỗi kết nối thật (VD bị từ chối) nếu có, thay vì nuốt im lặng
            _stream = _client.GetStream();
            _stream.ReadTimeout = _timeoutMs;
            _stream.WriteTimeout = _timeoutMs;
            // CỐ Ý không đặt Connected ở đây — mở socket TCP thành công chỉ chứng minh PLC đang lắng nghe ở
            // đúng IP/Port, KHÔNG chứng minh khung 3E được PLC hiểu và phản hồi đúng. Xác nhận đúng nghĩa sau
            // lần đọc/ghi thành công đầu tiên (cùng nguyên tắc ModbusRtuDriver, CLAUDE.md mục 3).
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
            var response = await SendAndReceiveAsync(request, isRead: true, isBitUnit: true, pointCount: 1, ct);
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
            var response = await SendAndReceiveAsync(request, isRead: false, isBitUnit: true, pointCount: 1, ct);
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
            var response = await SendAndReceiveAsync(request, isRead: true, isBitUnit: false, pointCount: 1, ct);
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
            var response = await SendAndReceiveAsync(request, isRead: false, isBitUnit: false, pointCount: 1, ct);
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
            var response = await SendAndReceiveAsync(request, isRead: true, isBitUnit: false, pointCount: count, ct);
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
            var response = await SendAndReceiveAsync(request, isRead: false, isBitUnit: false, pointCount: values.Count, ct);
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

    private async Task<byte[]> SendAndReceiveAsync(byte[] request, bool isRead, bool isBitUnit, int pointCount, CancellationToken ct)
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
            if (read == 0) throw new IOException("Kết nối TCP tới PLC đã đóng giữa chừng khi đang đọc phản hồi MC Protocol.");
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
