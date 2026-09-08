using EolTester.Core.Enums;

namespace EolTester.Communication;

public interface IPlcCommunicationDriver : IAsyncDisposable
{
    ConnectionState State { get; }
    event EventHandler<ConnectionState>? StateChanged;

    Task ConnectAsync(CancellationToken ct);
    Task DisconnectAsync();

    Task<bool> ReadDiscreteAsync(IoAddress address, CancellationToken ct);
    Task WriteDiscreteAsync(IoAddress address, bool value, CancellationToken ct);
    Task<int> ReadRegisterAsync(IoAddress address, CancellationToken ct);
    Task WriteRegisterAsync(IoAddress address, int value, CancellationToken ct);

    /// <summary>Đọc nhiều thanh ghi liên tiếp trong 1 giao dịch (VD Modbus FC03) — dùng cho vòng polling
    /// khối thay vì lặp N lần ReadRegisterAsync (mỗi lần là 1 request/response round-trip riêng trên bus
    /// RS485, rất chậm — xem CLAUDE.md mục "Lệnh thường dùng"/lịch sử phiên làm việc). Bộ gọi (PlcPollingService)
    /// chịu trách nhiệm chia nhỏ <paramref name="count"/> theo đúng giới hạn của giao thức (Modbus RTU FC03
    /// tối đa 125 thanh ghi/request) trước khi gọi.</summary>
    Task<int[]> ReadRegistersAsync(IoAddress startAddress, int count, CancellationToken ct);

    /// <summary>Ghi nhiều thanh ghi liên tiếp trong 1 giao dịch (VD Modbus FC16) — đối xứng với
    /// <see cref="ReadRegistersAsync"/>. Bộ gọi chịu trách nhiệm chia nhỏ theo giới hạn giao thức (Modbus RTU
    /// FC16 tối đa 123 thanh ghi/request).</summary>
    Task WriteRegistersAsync(IoAddress startAddress, IReadOnlyList<int> values, CancellationToken ct);
}
