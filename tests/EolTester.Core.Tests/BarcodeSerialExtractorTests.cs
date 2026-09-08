namespace EolTester.Core.Tests;

public class BarcodeSerialExtractorTests
{
    [Fact]
    public void TryExtractSerialNumber_NormalRange_ReturnsParsedNumber()
    {
        Assert.True(BarcodeSerialExtractor.TryExtractSerialNumber("ABC00123XYZ", 4, 8, out var value));
        Assert.Equal(123, value);
    }

    [Fact]
    public void TryExtractSerialNumber_LeadingZeros_ParsesAsInt()
    {
        Assert.True(BarcodeSerialExtractor.TryExtractSerialNumber("00042", 1, 5, out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryExtractSerialNumber_EndIndexBeyondStringLength_ClampsToStringEnd()
    {
        Assert.True(BarcodeSerialExtractor.TryExtractSerialNumber("AB007", 3, 20, out var value));
        Assert.Equal(7, value);
    }

    [Fact]
    public void TryExtractSerialNumber_StartIndexBeyondStringLength_ReturnsFalse()
    {
        Assert.False(BarcodeSerialExtractor.TryExtractSerialNumber("AB", 5, 8, out _));
    }

    [Fact]
    public void TryExtractSerialNumber_NonNumericSegment_ReturnsFalse()
    {
        Assert.False(BarcodeSerialExtractor.TryExtractSerialNumber("ABCXYZDEF", 4, 6, out _));
    }

    [Fact]
    public void TryExtractSerialNumber_EndIndexLessThanStartIndex_ReturnsFalse()
    {
        Assert.False(BarcodeSerialExtractor.TryExtractSerialNumber("ABC123", 5, 2, out _));
    }

    [Fact]
    public void TryExtractSerialNumber_NullOrEmptyBarcode_ReturnsFalse()
    {
        Assert.False(BarcodeSerialExtractor.TryExtractSerialNumber(null, 1, 5, out _));
        Assert.False(BarcodeSerialExtractor.TryExtractSerialNumber("", 1, 5, out _));
    }

    [Fact]
    public void TryExtractSerialNumber_StartIndexZeroOrNegative_ReturnsFalse()
    {
        Assert.False(BarcodeSerialExtractor.TryExtractSerialNumber("ABC123", 0, 5, out _));
        Assert.False(BarcodeSerialExtractor.TryExtractSerialNumber("ABC123", -1, 5, out _));
    }
}
