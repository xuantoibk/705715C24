using EolTester.Communication.Mc;

namespace EolTester.Communication.Tests;

public class Mc3EFrameCodecTests
{
    public static IEnumerable<object[]> Codecs()
    {
        yield return [new Mc3EBinaryFrameCodec()];
        yield return [new Mc3EAsciiFrameCodec()];
    }

    [Theory]
    [MemberData(nameof(Codecs))]
    public void ReadWordRequest_RoundTrips_ThroughSlaveParsing(IMc3EFrameCodec codec)
    {
        var request = codec.EncodeReadWordRequest(headDevice: 1005, pointCount: 3);

        var header = request[..codec.RequestHeaderLength];
        var dataLength = codec.GetRequestDataLength(header);
        Assert.Equal(request.Length - codec.RequestHeaderLength, dataLength);

        var parsed = codec.ParseRequest(request);
        Assert.False(parsed.IsWrite);
        Assert.False(parsed.IsBitUnit);
        Assert.Equal(1005, parsed.HeadDevice);
        Assert.Equal(3, parsed.PointCount);
    }

    [Theory]
    [MemberData(nameof(Codecs))]
    public void WriteWordRequest_RoundTrips_ThroughSlaveParsing(IMc3EFrameCodec codec)
    {
        ushort[] values = [111, 222, 65535];
        var request = codec.EncodeWriteWordRequest(headDevice: 2000, values);

        var parsed = codec.ParseRequest(request);
        Assert.True(parsed.IsWrite);
        Assert.False(parsed.IsBitUnit);
        Assert.Equal(2000, parsed.HeadDevice);
        Assert.Equal(values, parsed.WriteWordValues);
    }

    [Theory]
    [MemberData(nameof(Codecs))]
    public void ReadWordResponse_RoundTrips_ThroughMasterDecoding(IMc3EFrameCodec codec)
    {
        ushort[] values = [4242, 0, 65535];
        var response = codec.EncodeReadWordResponse(values);

        var header = response[..codec.ResponseHeaderLength];
        var dataLength = codec.GetResponseDataLength(header);
        Assert.Equal(response.Length - codec.ResponseHeaderLength, dataLength);

        var decoded = codec.DecodeReadWordResponse(response, values.Length);
        Assert.Equal(values, decoded);
    }

    [Theory]
    [MemberData(nameof(Codecs))]
    public void WriteAckResponse_DoesNotThrow(IMc3EFrameCodec codec)
    {
        var response = codec.EncodeWriteAckResponse();
        codec.ValidateWriteResponse(response); // không ném là đạt
    }

    [Theory]
    [MemberData(nameof(Codecs))]
    public void ErrorResponse_ThrowsMcProtocolExceptionWithEndCode(IMc3EFrameCodec codec)
    {
        var response = codec.EncodeErrorResponse(0x4031);
        var ex = Assert.Throws<McProtocolException>(() => codec.ValidateWriteResponse(response));
        Assert.Equal((ushort)0x4031, ex.EndCode);
    }

    [Theory]
    [MemberData(nameof(Codecs))]
    public void WriteBitRequest_RoundTrips_ThroughSlaveParsing(IMc3EFrameCodec codec)
    {
        bool[] values = [true, false, true];
        var request = codec.EncodeWriteBitRequest(headDevice: 10, values);

        var parsed = codec.ParseRequest(request);
        Assert.True(parsed.IsWrite);
        Assert.True(parsed.IsBitUnit);
        Assert.Equal(10, parsed.HeadDevice);
        Assert.Equal(values, parsed.WriteBitValues);
    }

    [Theory]
    [MemberData(nameof(Codecs))]
    public void ReadBitResponse_RoundTrips_ThroughMasterDecoding(IMc3EFrameCodec codec)
    {
        bool[] values = [true, false, true, true];
        var response = codec.EncodeReadBitResponse(values);

        var decoded = codec.DecodeReadBitResponse(response, values.Length);
        Assert.Equal(values, decoded);
    }
}
