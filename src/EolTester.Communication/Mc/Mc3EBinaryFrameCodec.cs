namespace EolTester.Communication.Mc;

/// <summary>
/// Khung 3E dạng Binary (theo tài liệu giao thức truyền thông MELSEC của Mitsubishi) — dùng chung cho MC
/// Protocol (khi <c>McFrameFormat.Binary</c>) và SLMP (luôn binary). Chỉ thao tác trên thiết bị "D" (word
/// register) cho batch read/write — khớp quy ước địa chỉ Dxxxx dùng xuyên suốt dự án. Xem
/// <see cref="IMc3EFrameCodec"/> về giới hạn/giả định của phần thao tác bit (M-device).
/// </summary>
public sealed class Mc3EBinaryFrameCodec : IMc3EFrameCodec
{
    private const byte DeviceCodeD = 0xA8;
    private const byte DeviceCodeM = 0x90;
    private const ushort CommandBatchReadWrite = 0x0401;
    private const ushort CommandBatchWrite = 0x1401;
    private const ushort SubCommandWordUnits = 0x0000;
    private const ushort SubCommandBitUnits = 0x0001;

    private readonly byte _networkNumber;
    private readonly byte _pcNumber;
    private readonly ushort _destModuleIo;
    private readonly byte _destModuleStation;
    private readonly ushort _monitoringTimer;

    /// <summary>Giá trị mặc định theo quy ước phổ biến trong tài liệu Mitsubishi (network=0, PC=0xFF,
    /// dest module IO=0x03FF, dest station=0, monitoring timer=4 (~1s, đơn vị 250ms)) — GIẢ ĐỊNH cần đối
    /// chiếu khi có PLC thật (xem CLAUDE.md mục 13).</summary>
    public Mc3EBinaryFrameCodec(byte networkNumber = 0x00, byte pcNumber = 0xFF, ushort destModuleIo = 0x03FF, byte destModuleStation = 0x00, ushort monitoringTimer = 4)
    {
        _networkNumber = networkNumber;
        _pcNumber = pcNumber;
        _destModuleIo = destModuleIo;
        _destModuleStation = destModuleStation;
        _monitoringTimer = monitoringTimer;
    }

    public int ResponseHeaderLength => 9;
    public int RequestHeaderLength => 9;

    public int GetResponseDataLength(byte[] header) => BitConverter.ToUInt16(header, 7);
    public int GetRequestDataLength(byte[] header) => BitConverter.ToUInt16(header, 7);

    public byte[] EncodeReadWordRequest(int headDevice, int pointCount) =>
        BuildRequest(CommandBatchReadWrite, SubCommandWordUnits, DeviceCodeD, headDevice, (ushort)pointCount, null);

    public byte[] EncodeWriteWordRequest(int headDevice, ushort[] values) =>
        BuildRequest(CommandBatchWrite, SubCommandWordUnits, DeviceCodeD, headDevice, (ushort)values.Length, EncodeWords(values));

    public byte[] EncodeReadBitRequest(int headDevice, int pointCount) =>
        BuildRequest(CommandBatchReadWrite, SubCommandBitUnits, DeviceCodeM, headDevice, (ushort)pointCount, null);

    public byte[] EncodeWriteBitRequest(int headDevice, bool[] values) =>
        BuildRequest(CommandBatchWrite, SubCommandBitUnits, DeviceCodeM, headDevice, (ushort)values.Length, EncodeBitsAsBytes(values));

    private byte[] BuildRequest(ushort command, ushort subCommand, byte deviceCode, int headDevice, ushort pointCount, byte[]? writeData)
    {
        using var commandData = new MemoryStream();
        commandData.Write(BitConverter.GetBytes(_monitoringTimer));
        commandData.Write(BitConverter.GetBytes(command));
        commandData.Write(BitConverter.GetBytes(subCommand));
        commandData.Write(BitConverter.GetBytes(headDevice), 0, 3);
        commandData.WriteByte(deviceCode);
        commandData.Write(BitConverter.GetBytes(pointCount));
        if (writeData is not null) commandData.Write(writeData);

        using var frame = new MemoryStream();
        frame.WriteByte(0x50);
        frame.WriteByte(0x00);
        frame.WriteByte(_networkNumber);
        frame.WriteByte(_pcNumber);
        frame.Write(BitConverter.GetBytes(_destModuleIo));
        frame.WriteByte(_destModuleStation);
        frame.Write(BitConverter.GetBytes((ushort)commandData.Length));
        frame.Write(commandData.ToArray());
        return frame.ToArray();
    }

    public ushort[] DecodeReadWordResponse(byte[] fullResponse, int expectedCount)
    {
        ValidateSubheader(fullResponse, isRequest: false);
        var endCode = BitConverter.ToUInt16(fullResponse, 9);
        if (endCode != 0) throw new McProtocolException(endCode);

        var result = new ushort[expectedCount];
        for (var i = 0; i < expectedCount; i++)
            result[i] = BitConverter.ToUInt16(fullResponse, 11 + i * 2);
        return result;
    }

    public bool[] DecodeReadBitResponse(byte[] fullResponse, int expectedCount)
    {
        ValidateSubheader(fullResponse, isRequest: false);
        var endCode = BitConverter.ToUInt16(fullResponse, 9);
        if (endCode != 0) throw new McProtocolException(endCode);

        var result = new bool[expectedCount];
        for (var i = 0; i < expectedCount; i++)
            result[i] = fullResponse[11 + i] != 0;
        return result;
    }

    public void ValidateWriteResponse(byte[] fullResponse)
    {
        ValidateSubheader(fullResponse, isRequest: false);
        var endCode = BitConverter.ToUInt16(fullResponse, 9);
        if (endCode != 0) throw new McProtocolException(endCode);
    }

    public McParsedRequest ParseRequest(byte[] fullRequest)
    {
        ValidateSubheader(fullRequest, isRequest: true);
        var command = BitConverter.ToUInt16(fullRequest, 11);
        var subCommand = BitConverter.ToUInt16(fullRequest, 13);
        var headDeviceBytes = new byte[4];
        Array.Copy(fullRequest, 15, headDeviceBytes, 0, 3);
        var headDevice = BitConverter.ToInt32(headDeviceBytes);
        var pointCount = BitConverter.ToUInt16(fullRequest, 19);
        var isBitUnit = subCommand == SubCommandBitUnits;
        var isWrite = command == CommandBatchWrite;

        if (!isWrite)
            return new McParsedRequest(false, isBitUnit, headDevice, pointCount, null, null);

        if (isBitUnit)
        {
            var bitValues = new bool[pointCount];
            for (var i = 0; i < pointCount; i++) bitValues[i] = fullRequest[21 + i] != 0;
            return new McParsedRequest(true, true, headDevice, pointCount, null, bitValues);
        }
        else
        {
            var wordValues = new ushort[pointCount];
            for (var i = 0; i < pointCount; i++) wordValues[i] = BitConverter.ToUInt16(fullRequest, 21 + i * 2);
            return new McParsedRequest(true, false, headDevice, pointCount, wordValues, null);
        }
    }

    public byte[] EncodeReadWordResponse(ushort[] values) => BuildResponse(0, EncodeWords(values));
    public byte[] EncodeReadBitResponse(bool[] values) => BuildResponse(0, EncodeBitsAsBytes(values));
    public byte[] EncodeWriteAckResponse() => BuildResponse(0, null);
    public byte[] EncodeErrorResponse(ushort endCode) => BuildResponse(endCode, null);

    private byte[] BuildResponse(ushort endCode, byte[]? data)
    {
        using var dataSection = new MemoryStream();
        dataSection.Write(BitConverter.GetBytes(endCode));
        if (data is not null) dataSection.Write(data);

        using var frame = new MemoryStream();
        frame.WriteByte(0xD0);
        frame.WriteByte(0x00);
        frame.WriteByte(_networkNumber);
        frame.WriteByte(_pcNumber);
        frame.Write(BitConverter.GetBytes(_destModuleIo));
        frame.WriteByte(_destModuleStation);
        frame.Write(BitConverter.GetBytes((ushort)dataSection.Length));
        frame.Write(dataSection.ToArray());
        return frame.ToArray();
    }

    private static byte[] EncodeWords(ushort[] values)
    {
        var result = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
            BitConverter.GetBytes(values[i]).CopyTo(result, i * 2);
        return result;
    }

    private static byte[] EncodeBitsAsBytes(bool[] values)
    {
        var result = new byte[values.Length];
        for (var i = 0; i < values.Length; i++) result[i] = (byte)(values[i] ? 1 : 0);
        return result;
    }

    private static void ValidateSubheader(byte[] frame, bool isRequest)
    {
        var expected0 = (byte)(isRequest ? 0x50 : 0xD0);
        if (frame.Length < 9 || frame[0] != expected0 || frame[1] != 0x00)
            throw new FormatException("Khung 3E (MC Protocol/SLMP) không hợp lệ — sai subheader.");
    }
}
