namespace EolTester.Communication.Tests;

public class ModbusWordAddressTests
{
    [Fact]
    public void Parse_WholeWord_ReturnsNullBitIndex()
    {
        var addr = ModbusWordAddress.Parse("D1000");
        Assert.Equal(1000, addr.WordAddress);
        Assert.Null(addr.BitIndex);
    }

    [Fact]
    public void Parse_BitAddress_ReturnsBitIndex()
    {
        var addr = ModbusWordAddress.Parse("D1000.1");
        Assert.Equal(1000, addr.WordAddress);
        Assert.Equal(1, addr.BitIndex);
    }

    [Theory]
    [InlineData("D1000", "D1000")]
    [InlineData("D1000.0", "D1000.0")]
    [InlineData("D1000.15", "D1000.15")]
    public void Parse_ThenToString_RoundTrips(string input, string expected)
    {
        var addr = ModbusWordAddress.Parse(input);
        Assert.Equal(expected, addr.ToString());
    }

    [Theory]
    [InlineData("D1000.A", 10)]
    [InlineData("D1000.B", 11)]
    [InlineData("D1000.F", 15)]
    [InlineData("D1000.a", 10)]
    [InlineData("D1000.f", 15)]
    public void Parse_HexBitIndex_ReturnsDecimalBitIndex(string input, int expectedBitIndex)
    {
        var addr = ModbusWordAddress.Parse(input);
        Assert.Equal(1000, addr.WordAddress);
        Assert.Equal(expectedBitIndex, addr.BitIndex);
    }

    [Theory]
    [InlineData("X100")]
    [InlineData("D")]
    [InlineData("D1000.16")]
    [InlineData("D1000.-1")]
    [InlineData("")]
    [InlineData("Dabc")]
    public void Parse_InvalidInput_Throws(string input)
    {
        Assert.Throws<FormatException>(() => ModbusWordAddress.Parse(input));
    }

    [Fact]
    public void TryParse_ValidDAddress_ReturnsTrueAndParsedValue()
    {
        Assert.True(ModbusWordAddress.TryParse("D1005.1", out var addr));
        Assert.Equal(1005, addr.WordAddress);
        Assert.Equal(1, addr.BitIndex);
    }

    [Theory]
    [InlineData("X100")]
    [InlineData("10000")]
    [InlineData("")]
    public void TryParse_LegacyOrInvalidAddress_ReturnsFalseWithoutThrowing(string input)
    {
        Assert.False(ModbusWordAddress.TryParse(input, out var addr));
        Assert.Equal(default, addr);
    }
}
