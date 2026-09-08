namespace EolTester.Configuration.Models;

public enum ProtocolType
{
    ModbusRtu,
    McProtocol,
    Slmp,
    CcLink,
    EthernetIp
}

public enum ModbusParity
{
    None,
    Odd,
    Even
}

public enum ModbusRole
{
    Master,
    Slave
}

/// <summary>Định dạng khung dữ liệu MC Protocol (3E frame) — cấu hình được per-connection, mặc định Binary.
/// SLMP luôn dùng binary theo chuẩn của nó, không có lựa chọn này.</summary>
public enum McFrameFormat
{
    Binary,
    Ascii
}

public sealed class ModbusRegisterBlock
{
    public int StartAddress { get; set; }
    public int RegisterCount { get; set; }
}

public sealed class ConnectionSettings
{
    public ProtocolType Protocol { get; set; } = ProtocolType.ModbusRtu;
    public ModbusRole Role { get; set; } = ModbusRole.Master;

    // ----- Field riêng cho Protocol == ModbusRtu (serial) -----
    public string ComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public ModbusParity Parity { get; set; } = ModbusParity.None;
    public int DataBits { get; set; } = 8;
    public int StopBits { get; set; } = 1;

    public int StationAddress { get; set; } = 1;

    public int MySlaveId { get; set; } = 2;

    // ----- Field riêng cho Protocol ∈ {McProtocol, Slmp} (Ethernet/TCP) -----
    /// <summary>Master: IP của PLC cần kết nối tới. Slave/Adapter: IP cục bộ để lắng nghe (thường để trống/0.0.0.0).</summary>
    public string IpAddress { get; set; } = "192.168.0.10";

    /// <summary>Port TCP — mặc định 5000 theo quy ước phổ biến MC 3E Binary/SLMP (GIẢ ĐỊNH, cần đối chiếu khi có PLC thật, xem CLAUDE.md mục 13).</summary>
    public int Port { get; set; } = 5000;

    /// <summary>Chỉ áp dụng khi Protocol == McProtocol — SLMP luôn binary.</summary>
    public McFrameFormat McFrameFormat { get; set; } = McFrameFormat.Binary;

    public int PollingIntervalMs { get; set; } = 500;
    public int TimeoutMs { get; set; } = 1000;
    public int RetryCount { get; set; } = 3;
    public ModbusRegisterBlock InputBlock { get; set; } = new() { StartAddress = 1000, RegisterCount = 40 };
    public ModbusRegisterBlock OutputBlock { get; set; } = new() { StartAddress = 1100, RegisterCount = 20 };
}
