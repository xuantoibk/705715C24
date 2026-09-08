using EolTester.Core.Enums;

namespace EolTester.Communication;

/// <summary>
/// Vai trò Slave/Adapter trung lập theo giao thức — PC lắng nghe bị động, PLC (Master) thật chủ động gửi
/// request đọc/ghi tới. Tách biệt khỏi <see cref="IPlcCommunicationDriver"/> (Master, timing/polling chủ động)
/// vì luồng điều khiển ngược nhau (xem CLAUDE.md mục "Kiến trúc lõi: Bảng thanh ghi trung gian"). Mọi cài đặt
/// (Modbus RTU, MC Protocol, SLMP...) đọc/ghi thẳng vào <see cref="PlcRegisterImage"/> dùng chung với Master.
/// </summary>
public interface IPlcSlaveService : IAsyncDisposable
{
    ConnectionState State { get; }
    event EventHandler<ConnectionState>? StateChanged;

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
}
