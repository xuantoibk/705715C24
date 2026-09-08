using System.Net;
using System.Net.Sockets;
using EolTester.Core.Enums;

namespace EolTester.Communication.Mc;

/// <summary>
/// PC làm Adapter (Slave) cho MC Protocol — bị động lắng nghe TCP, PLC (Master) thật chủ động gửi request đọc/
/// ghi khung 3E tới. Cùng vai trò kiến trúc với <see cref="ModbusSlaveService"/> nhưng transport là TCP socket
/// thay vì cổng COM/NModbus. Thiết bị "D" (word) đọc/ghi thẳng vào <see cref="PlcRegisterImage"/> dùng chung
/// với Master — cùng nguồn dữ liệu, cùng nguyên tắc "bảng thanh ghi trung gian" (xem CLAUDE.md). Thiết bị "M"
/// (bit) dùng mảng bool riêng trong bộ nhớ (giống <c>ArrayBoolPointSource</c> của Modbus coil) — chưa có nhu
/// cầu cụ thể liên kết với PlcRegisterImage, mở rộng khi cần.
/// </summary>
public sealed class McProtocolSlaveService : IPlcSlaveService
{
    private readonly PlcRegisterImage _registerTable;
    private readonly bool[] _bitDeviceStore = new bool[ushort.MaxValue + 1];
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private ConnectionState _state = ConnectionState.Disconnected;

    private string _ipAddress = "0.0.0.0";
    private int _port = 5000;
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

    /// <summary>Port TCP thực tế đang lắng nghe — khác <c>_port</c> đã cấu hình khi dùng port 0 (OS tự cấp phát
    /// port trống), dùng cho unit test loopback không cần chiếm 1 port cố định.</summary>
    public int? ListeningPort { get; private set; }

    public McProtocolSlaveService(PlcRegisterImage registerTable)
    {
        _registerTable = registerTable;
    }

    public void Configure(string ipAddress, int port, McFrameFormatKind frameFormat)
    {
        _ipAddress = ipAddress;
        _port = port;
        _codec = frameFormat == McFrameFormatKind.Ascii ? new Mc3EAsciiFrameCodec() : new Mc3EBinaryFrameCodec();
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        State = ConnectionState.Connecting;
        try
        {
            var address = string.IsNullOrWhiteSpace(_ipAddress) || _ipAddress == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(_ipAddress);
            _listener = new TcpListener(address, _port);
            _listener.Start();
            ListeningPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _acceptTask = AcceptLoopAsync(_cts.Token);
            State = ConnectionState.Connected;
            return Task.CompletedTask;
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
        _listener?.Stop();
        State = ConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var client = await _listener!.AcceptTcpClientAsync(ct);
                await using var stream = client.GetStream();
                await ServeConnectionAsync(stream, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Client đóng kết nối bất thường/lỗi khung — bỏ qua, quay lại chờ kết nối kế tiếp (giống
                // tinh thần catch-all của PlcPollingService.PollLoopAsync, không để chết hẳn vòng lặp nền).
            }
        }
    }

    /// <summary>
    /// Vòng lặp phục vụ 1 kết nối TCP: đọc 1 request, phản hồi, lặp lại tới khi client đóng kết nối. Tách
    /// riêng khỏi <see cref="AcceptLoopAsync"/> để unit/integration test có thể gọi trực tiếp với 1 cặp
    /// TCP loopback (giống pattern <c>ModbusSlaveService.StartWithStreamAsync</c> dùng cho test Modbus).
    /// </summary>
    internal async Task ServeConnectionAsync(Stream stream, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            byte[] header;
            try
            {
                header = await ReadExactAsync(stream, _codec.RequestHeaderLength, ct);
            }
            catch (IOException)
            {
                return;
            }

            int dataLength;
            try
            {
                dataLength = _codec.GetRequestDataLength(header);
            }
            catch (FormatException)
            {
                return;
            }

            var rest = await ReadExactAsync(stream, dataLength, ct);
            var full = new byte[header.Length + rest.Length];
            header.CopyTo(full, 0);
            rest.CopyTo(full, header.Length);

            byte[] response;
            try
            {
                response = HandleRequest(_codec.ParseRequest(full));
            }
            catch (FormatException)
            {
                continue; // request lỗi định dạng — bỏ qua, chờ request kế tiếp thay vì đóng cả kết nối.
            }

            await stream.WriteAsync(response, ct);
        }
    }

    private byte[] HandleRequest(McParsedRequest request)
    {
        if (request.IsBitUnit)
        {
            if (request.IsWrite)
            {
                for (var i = 0; i < request.PointCount; i++)
                    _bitDeviceStore[request.HeadDevice + i] = request.WriteBitValues![i];
                return _codec.EncodeWriteAckResponse();
            }

            var bitValues = new bool[request.PointCount];
            for (var i = 0; i < request.PointCount; i++) bitValues[i] = _bitDeviceStore[request.HeadDevice + i];
            return _codec.EncodeReadBitResponse(bitValues);
        }

        if (request.IsWrite)
        {
            for (var i = 0; i < request.PointCount; i++)
                _registerTable.TryUpdateWord(request.HeadDevice + i, request.WriteWordValues![i]);
            return _codec.EncodeWriteAckResponse();
        }

        var wordValues = new ushort[request.PointCount];
        for (var i = 0; i < request.PointCount; i++)
        {
            _registerTable.TryGetWord(request.HeadDevice + i, out var v);
            wordValues[i] = v;
        }
        return _codec.EncodeReadWordResponse(wordValues);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (read == 0) throw new IOException("Kết nối TCP đã đóng giữa chừng khi đang đọc request MC Protocol.");
            offset += read;
        }
        return buffer;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        if (_acceptTask is not null)
        {
            try { await _acceptTask; } catch { /* ignore — hủy do dừng service */ }
        }
        State = ConnectionState.Disconnected;
    }
}
