using System.Net;
using System.Net.Sockets;
using EolTester.Communication.Mc;
using EolTester.Core.Enums;

namespace EolTester.Communication.Slmp;

/// <summary>
/// PC làm Adapter (Slave) cho SLMP — cùng kiến trúc <see cref="McProtocolSlaveService"/> (lắng nghe TCP bị
/// động, đọc/ghi thẳng vào <see cref="PlcRegisterImage"/> dùng chung với Master), nhưng luôn Binary (SLMP
/// không có ASCII) nên tái dùng thẳng <see cref="Mc3EBinaryFrameCodec"/> không qua lựa chọn định dạng.
/// </summary>
public sealed class SlmpSlaveService : IPlcSlaveService
{
    private readonly PlcRegisterImage _registerTable;
    private readonly bool[] _bitDeviceStore = new bool[ushort.MaxValue + 1];
    private readonly IMc3EFrameCodec _codec = new Mc3EBinaryFrameCodec();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private ConnectionState _state = ConnectionState.Disconnected;

    private string _ipAddress = "0.0.0.0";
    private int _port = 5001;

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

    public int? ListeningPort { get; private set; }

    public SlmpSlaveService(PlcRegisterImage registerTable)
    {
        _registerTable = registerTable;
    }

    public void Configure(string ipAddress, int port)
    {
        _ipAddress = ipAddress;
        _port = port;
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
                // Cùng lý do McProtocolSlaveService — không để chết hẳn vòng lặp nền vì 1 kết nối lỗi.
            }
        }
    }

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
                continue;
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
            if (read == 0) throw new IOException("Kết nối TCP đã đóng giữa chừng khi đang đọc request SLMP.");
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
            try { await _acceptTask; } catch { /* ignore */ }
        }
        State = ConnectionState.Disconnected;
    }
}
