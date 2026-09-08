namespace EolTester.Communication.Mc;

/// <summary>PLC trả về end code khác 0 (lỗi) trong khung phản hồi 3E — dùng chung cho MC Protocol lẫn SLMP
/// (2 giao thức chia sẻ cùng cấu trúc khung 3E, xem <see cref="Mc3EBinaryFrameCodec"/>).</summary>
public sealed class McProtocolException(ushort endCode) : Exception($"PLC trả về lỗi (end code 0x{endCode:X4}) khi xử lý request MC Protocol/SLMP.")
{
    public ushort EndCode { get; } = endCode;
}
