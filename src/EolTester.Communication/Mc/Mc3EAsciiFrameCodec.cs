using System.Globalization;
using System.Text;

namespace EolTester.Communication.Mc;

/// <summary>
/// Khung 3E dạng ASCII cho MC Protocol (<c>McFrameFormat.Ascii</c> — SLMP luôn dùng Binary, xem
/// <see cref="Mc3EBinaryFrameCodec"/>, không dùng codec này). Mọi trường số được biểu diễn bằng ký tự hex
/// ASCII (không đảo byte thứ tự little-endian như Binary), device code là 2 ký tự ("D*" cho thanh ghi D, "M*"
/// cho relay M — quy ước phổ biến của Mitsubishi khi mã thiết bị chỉ có 1 chữ cái).
/// <para>
/// <b>GIẢ ĐỊNH cần xác nhận khi có PLC MC thật</b> (xem CLAUDE.md mục 13): tài liệu MC Protocol gốc quy định số
/// thiết bị (device number) của 1 số loại thiết bị (VD D) dùng hệ thập phân chứ không phải hex trong khung
/// ASCII — codec này dùng thống nhất hex 6 ký tự cho đơn giản/nhất quán nội bộ (tự mã hóa/giải mã đối xứng,
/// verify được bằng unit test round-trip), có thể cần đổi sang thập phân khi đối chiếu với PLC thật.
/// </para>
/// </summary>
public sealed class Mc3EAsciiFrameCodec : IMc3EFrameCodec
{
    private const string DeviceCodeD = "D*";
    private const string DeviceCodeM = "M*";
    private const string CommandBatchReadWrite = "0401";
    private const string CommandBatchWrite = "1401";
    private const string SubCommandWordUnits = "0000";
    private const string SubCommandBitUnits = "0001";

    private readonly byte _networkNumber;
    private readonly byte _pcNumber;
    private readonly ushort _destModuleIo;
    private readonly byte _destModuleStation;
    private readonly ushort _monitoringTimer;

    public Mc3EAsciiFrameCodec(byte networkNumber = 0x00, byte pcNumber = 0xFF, ushort destModuleIo = 0x03FF, byte destModuleStation = 0x00, ushort monitoringTimer = 4)
    {
        _networkNumber = networkNumber;
        _pcNumber = pcNumber;
        _destModuleIo = destModuleIo;
        _destModuleStation = destModuleStation;
        _monitoringTimer = monitoringTimer;
    }

    // Header cố định trước field "độ dài": "5000"(4) + network(2) + pcNo(2) + destIo(4) + destStation(2) + dataLength(4) = 18 ký tự.
    public int RequestHeaderLength => 18;
    public int ResponseHeaderLength => 18;

    public int GetRequestDataLength(byte[] header) => int.Parse(AsciiOf(header)[14..18], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    public int GetResponseDataLength(byte[] header) => int.Parse(AsciiOf(header)[14..18], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    public byte[] EncodeReadWordRequest(int headDevice, int pointCount) =>
        BuildRequest(CommandBatchReadWrite, SubCommandWordUnits, DeviceCodeD, headDevice, pointCount, null);

    public byte[] EncodeWriteWordRequest(int headDevice, ushort[] values) =>
        BuildRequest(CommandBatchWrite, SubCommandWordUnits, DeviceCodeD, headDevice, values.Length, EncodeWordsHex(values));

    public byte[] EncodeReadBitRequest(int headDevice, int pointCount) =>
        BuildRequest(CommandBatchReadWrite, SubCommandBitUnits, DeviceCodeM, headDevice, pointCount, null);

    public byte[] EncodeWriteBitRequest(int headDevice, bool[] values) =>
        BuildRequest(CommandBatchWrite, SubCommandBitUnits, DeviceCodeM, headDevice, values.Length, EncodeBitsHex(values));

    private byte[] BuildRequest(string command, string subCommand, string deviceCode, int headDevice, int pointCount, string? writeDataHex)
    {
        var commandData = new StringBuilder();
        commandData.Append(_monitoringTimer.ToString("X4"));
        commandData.Append(command);
        commandData.Append(subCommand);
        commandData.Append(headDevice.ToString("X6"));
        commandData.Append(deviceCode);
        commandData.Append(pointCount.ToString("X4"));
        if (writeDataHex is not null) commandData.Append(writeDataHex);

        var frame = new StringBuilder();
        frame.Append("5000");
        frame.Append(_networkNumber.ToString("X2"));
        frame.Append(_pcNumber.ToString("X2"));
        frame.Append(_destModuleIo.ToString("X4"));
        frame.Append(_destModuleStation.ToString("X2"));
        frame.Append(commandData.Length.ToString("X4"));
        frame.Append(commandData);
        return Encoding.ASCII.GetBytes(frame.ToString());
    }

    public ushort[] DecodeReadWordResponse(byte[] fullResponse, int expectedCount)
    {
        var text = ValidateSubheaderAndGetEndCode(fullResponse, isRequest: false, out var endCode);
        if (endCode != 0) throw new McProtocolException(endCode);

        var result = new ushort[expectedCount];
        for (var i = 0; i < expectedCount; i++)
            result[i] = ushort.Parse(text.Substring(22 + i * 4, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return result;
    }

    public bool[] DecodeReadBitResponse(byte[] fullResponse, int expectedCount)
    {
        var text = ValidateSubheaderAndGetEndCode(fullResponse, isRequest: false, out var endCode);
        if (endCode != 0) throw new McProtocolException(endCode);

        var result = new bool[expectedCount];
        for (var i = 0; i < expectedCount; i++)
            result[i] = text[22 + i] != '0';
        return result;
    }

    public void ValidateWriteResponse(byte[] fullResponse)
    {
        ValidateSubheaderAndGetEndCode(fullResponse, isRequest: false, out var endCode);
        if (endCode != 0) throw new McProtocolException(endCode);
    }

    public McParsedRequest ParseRequest(byte[] fullRequest)
    {
        // Sau 18 ký tự header (subheader+network+pcNo+destIo+destStation+dataLength) là phần command data:
        // monitoringTimer[18,22) + command[22,26) + subCommand[26,30) + headDevice[30,36) + deviceCode[36,38) + pointCount[38,42) + data từ [42,...).
        var text = ValidateSubheaderAndGetEndCode(fullRequest, isRequest: true, out _);
        var command = text.Substring(22, 4);
        var subCommand = text.Substring(26, 4);
        var headDevice = int.Parse(text.Substring(30, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var pointCount = int.Parse(text.Substring(38, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var isBitUnit = subCommand == SubCommandBitUnits;
        var isWrite = command == CommandBatchWrite;

        if (!isWrite)
            return new McParsedRequest(false, isBitUnit, headDevice, pointCount, null, null);

        if (isBitUnit)
        {
            var bitValues = new bool[pointCount];
            for (var i = 0; i < pointCount; i++) bitValues[i] = text[42 + i] != '0';
            return new McParsedRequest(true, true, headDevice, pointCount, null, bitValues);
        }
        else
        {
            var wordValues = new ushort[pointCount];
            for (var i = 0; i < pointCount; i++)
                wordValues[i] = ushort.Parse(text.Substring(42 + i * 4, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new McParsedRequest(true, false, headDevice, pointCount, wordValues, null);
        }
    }

    public byte[] EncodeReadWordResponse(ushort[] values) => BuildResponse(0, EncodeWordsHex(values));
    public byte[] EncodeReadBitResponse(bool[] values) => BuildResponse(0, EncodeBitsHex(values));
    public byte[] EncodeWriteAckResponse() => BuildResponse(0, null);
    public byte[] EncodeErrorResponse(ushort endCode) => BuildResponse(endCode, null);

    private byte[] BuildResponse(ushort endCode, string? dataHex)
    {
        var dataSection = new StringBuilder();
        dataSection.Append(endCode.ToString("X4"));
        if (dataHex is not null) dataSection.Append(dataHex);

        var frame = new StringBuilder();
        frame.Append("D000");
        frame.Append(_networkNumber.ToString("X2"));
        frame.Append(_pcNumber.ToString("X2"));
        frame.Append(_destModuleIo.ToString("X4"));
        frame.Append(_destModuleStation.ToString("X2"));
        frame.Append(dataSection.Length.ToString("X4"));
        frame.Append(dataSection);
        return Encoding.ASCII.GetBytes(frame.ToString());
    }

    private static string EncodeWordsHex(ushort[] values) => string.Concat(values.Select(v => v.ToString("X4")));
    private static string EncodeBitsHex(bool[] values) => string.Concat(values.Select(v => v ? "1" : "0"));

    private static string AsciiOf(byte[] bytes) => Encoding.ASCII.GetString(bytes);

    private string ValidateSubheaderAndGetEndCode(byte[] frame, bool isRequest, out ushort endCode)
    {
        var text = AsciiOf(frame);
        var expectedSubheader = isRequest ? "5000" : "D000";
        if (text.Length < 18 || text[..4] != expectedSubheader)
            throw new FormatException("Khung 3E ASCII (MC Protocol) không hợp lệ — sai subheader.");

        endCode = isRequest ? (ushort)0 : ushort.Parse(text.Substring(18, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return text;
    }
}
