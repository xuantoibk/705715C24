namespace EolTester.Communication.Mc;

/// <summary>Request đã giải mã phía Slave/Adapter — dùng để phục vụ request PLC Master thật gửi tới khi PC
/// làm Slave cho MC Protocol/SLMP.</summary>
public readonly record struct McParsedRequest(bool IsWrite, bool IsBitUnit, int HeadDevice, int PointCount, ushort[]? WriteWordValues, bool[]? WriteBitValues);

/// <summary>
/// Trừu tượng hóa khung 3E (dùng chung cho cả MC Protocol lẫn SLMP — 2 giao thức chia sẻ cùng cấu trúc khung
/// 3E theo tài liệu Mitsubishi, chỉ khác ở việc MC Protocol cho phép chọn Binary/ASCII còn SLMP luôn Binary).
/// Chỉ hỗ trợ thao tác trên thiết bị "D" (word register, batch read/write) — đúng quy ước địa chỉ Dxxxx đã
/// dùng xuyên suốt dự án (xem CLAUDE.md). Thao tác bit (M-device, dùng cho <see cref="IPlcCommunicationDriver.ReadDiscreteAsync"/>/
/// WriteDiscreteAsync — đường fallback hiếm gặp cho địa chỉ kiểu cũ không phải "Dxxxx") dùng quy ước ĐƠN GIẢN HÓA:
/// mỗi điểm chiếm nguyên 1 byte trong dữ liệu phản hồi (0x01/0x00) thay vì đóng gói nibble theo đúng chuẩn MC —
/// ĐÂY LÀ GIẢ ĐỊNH cần xác nhận/điều chỉnh khi có PLC MC/SLMP thật (xem CLAUDE.md mục 13), vì đường word-register
/// (D-register) mới là đường chính được dùng thật trong toàn bộ nghiệp vụ ứng dụng.
/// </summary>
public interface IMc3EFrameCodec
{
    // ----- Phía Master (McProtocolDriver/SlmpDriver) -----
    byte[] EncodeReadWordRequest(int headDevice, int pointCount);
    byte[] EncodeWriteWordRequest(int headDevice, ushort[] values);
    byte[] EncodeReadBitRequest(int headDevice, int pointCount);
    byte[] EncodeWriteBitRequest(int headDevice, bool[] values);

    /// <summary>Số byte cố định của phần header phản hồi (trước phần dữ liệu) — dùng để đọc đủ header từ socket trước khi biết độ dài còn lại.</summary>
    int ResponseHeaderLength { get; }

    /// <summary>Đọc "response data length" đã có trong header đã đọc được, để biết cần đọc thêm bao nhiêu byte nữa cho đủ 1 response hoàn chỉnh.</summary>
    int GetResponseDataLength(byte[] header);

    ushort[] DecodeReadWordResponse(byte[] fullResponse, int expectedCount);
    bool[] DecodeReadBitResponse(byte[] fullResponse, int expectedCount);
    void ValidateWriteResponse(byte[] fullResponse);

    // ----- Phía Slave/Adapter (McProtocolSlaveService/SlmpSlaveService) -----
    /// <summary>Số byte tối thiểu để nhận diện + biết độ dài phần còn lại của 1 request gửi tới.</summary>
    int RequestHeaderLength { get; }
    int GetRequestDataLength(byte[] header);
    McParsedRequest ParseRequest(byte[] fullRequest);
    byte[] EncodeReadWordResponse(ushort[] values);
    byte[] EncodeReadBitResponse(bool[] values);
    byte[] EncodeWriteAckResponse();
    byte[] EncodeErrorResponse(ushort endCode);
}
