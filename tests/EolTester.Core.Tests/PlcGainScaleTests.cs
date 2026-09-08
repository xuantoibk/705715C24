namespace EolTester.Core.Tests;

/// <summary>
/// Hệ số Gain (Scale) quyết định quy đổi giá trị thực &lt;-&gt; số nguyên thanh ghi PLC và số chữ số thập phân
/// hiển thị ở tab Set Spec./Main. Trước round rà soát này lớp <see cref="PlcGainScale"/> hoàn toàn chưa có
/// test — nhưng nó nằm trên đường tính Đạt/Không đạt của phép đo, cần khóa hành vi lại.
/// </summary>
public class PlcGainScaleTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(100, true)]
    [InlineData(1000, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(2, false)]
    [InlineData(50, false)]
    [InlineData(10000, false)]
    public void IsValid_OnlyAcceptsThePermittedGainSteps(int scale, bool expected)
    {
        Assert.Equal(expected, PlcGainScale.IsValid(scale));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(10, 1)]
    [InlineData(100, 2)]
    [InlineData(1000, 3)]
    [InlineData(0, 0)]      // giá trị lạ -> không có phần thập phân (an toàn, không ném)
    [InlineData(7, 0)]
    public void DecimalDigits_MatchesScaleMagnitude(int scale, int expectedDigits)
    {
        Assert.Equal(expectedDigits, PlcGainScale.DecimalDigits(scale));
    }

    [Theory]
    [InlineData(23.456, 1, "23")]
    [InlineData(23.456, 10, "23.5")]
    [InlineData(23.456, 100, "23.46")]
    [InlineData(23.4567, 1000, "23.457")]
    [InlineData(-0.5, 1, "-1")]      // AwayFromZero: -0.5 -> -1, không phải 0
    [InlineData(2.5, 1, "3")]         // AwayFromZero: 2.5 -> 3, không phải banker's rounding
    public void Round_UsesAwayFromZeroToScaleDigits(decimal value, int scale, string expected)
    {
        var rounded = PlcGainScale.Round(value, scale);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), rounded);
    }

    [Fact]
    public void Round_WithInvalidScale_DoesNotThrowAndRoundsToInteger()
    {
        // Scale không hợp lệ chỉ nên rơi về "0 chữ số thập phân", tuyệt đối không ném (vòng polling gọi mỗi tick).
        var rounded = PlcGainScale.Round(12.9m, scale: 0);
        Assert.Equal(13m, rounded);
    }
}
