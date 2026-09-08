using System.IO.Ports;
using EolTester.Communication.Mc;
using EolTester.Configuration.Models;

namespace EolTester.App.Services;

/// <summary>Quy đổi giữa enum cấu hình của app (chuẩn hóa combobox ở tab Setup) và kiểu <see cref="System.IO.Ports"/> mà SerialPort/NModbus cần.</summary>
public static class SerialSettingsMapping
{
    public static Parity ToSerialPortParity(this ModbusParity parity) => parity switch
    {
        ModbusParity.Odd => Parity.Odd,
        ModbusParity.Even => Parity.Even,
        _ => Parity.None,
    };

    public static StopBits ToSerialPortStopBits(this int stopBits) => stopBits switch
    {
        2 => StopBits.Two,
        _ => StopBits.One,
    };

    public static McFrameFormatKind ToMcFrameFormatKind(this McFrameFormat format) => format switch
    {
        McFrameFormat.Ascii => McFrameFormatKind.Ascii,
        _ => McFrameFormatKind.Binary,
    };
}
