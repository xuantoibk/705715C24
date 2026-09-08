namespace EolTester.Core.Tests;

public class CsvColumnSpecParserTests
{
    [Fact]
    public void Parse_Stt_ReturnsSequence()
    {
        Assert.IsType<CsvColumnSpec.Sequence>(CsvColumnSpecParser.Parse("<STT>"));
    }

    [Fact]
    public void Parse_SttLowercase_IsCaseInsensitive()
    {
        Assert.IsType<CsvColumnSpec.Sequence>(CsvColumnSpecParser.Parse("<stt>"));
    }

    [Fact]
    public void Parse_Barcode_ReturnsBarcodeColumn()
    {
        Assert.IsType<CsvColumnSpec.BarcodeColumn>(CsvColumnSpecParser.Parse("<barcode>"));
    }

    [Fact]
    public void Parse_Job_ReturnsJobIdColumn()
    {
        Assert.IsType<CsvColumnSpec.JobIdColumn>(CsvColumnSpecParser.Parse("<JOB>"));
    }

    [Fact]
    public void Parse_DateShorthand_ReturnsDateColumnWithNullFormat()
    {
        var spec = Assert.IsType<CsvColumnSpec.DateTimeColumn>(CsvColumnSpecParser.Parse("<date>"));
        Assert.False(spec.IsTime);
        Assert.Null(spec.CustomFormat);
    }

    [Fact]
    public void Parse_TimeShorthand_ReturnsTimeColumnWithNullFormat()
    {
        var spec = Assert.IsType<CsvColumnSpec.DateTimeColumn>(CsvColumnSpecParser.Parse("<time>"));
        Assert.True(spec.IsTime);
        Assert.Null(spec.CustomFormat);
    }

    [Fact]
    public void Parse_CompositeDateFormat_SubstitutesDotNetTokensAndKeepsSeparators()
    {
        var spec = Assert.IsType<CsvColumnSpec.DateTimeColumn>(CsvColumnSpecParser.Parse("<yyyy>-<mm>-<dd>"));
        Assert.False(spec.IsTime);
        Assert.Equal("yyyy-MM-dd", spec.CustomFormat);
    }

    [Fact]
    public void Parse_CompositeTimeFormat_SubstitutesDotNetTokens()
    {
        var spec = Assert.IsType<CsvColumnSpec.DateTimeColumn>(CsvColumnSpecParser.Parse("<hh>:<min>:<sec>"));
        Assert.True(spec.IsTime);
        Assert.Equal("HH:mm:ss", spec.CustomFormat);
    }

    [Fact]
    public void Parse_MixedDateAndTimeSubTokens_PrefersDateHeaderKeepsCombinedFormat()
    {
        var spec = Assert.IsType<CsvColumnSpec.DateTimeColumn>(CsvColumnSpecParser.Parse("<yyyy><mm><dd>_<hh><min><sec>"));
        Assert.False(spec.IsTime);
        Assert.Equal("yyyyMMdd_HHmmss", spec.CustomFormat);
    }

    [Fact]
    public void Parse_RegisterAddress_ReturnsRegisterColumn()
    {
        var spec = Assert.IsType<CsvColumnSpec.RegisterColumn>(CsvColumnSpecParser.Parse("D50"));
        Assert.Equal("D50", spec.RawText);
    }

    [Fact]
    public void Parse_RegisterBitAddress_ReturnsRegisterColumn()
    {
        var spec = Assert.IsType<CsvColumnSpec.RegisterColumn>(CsvColumnSpecParser.Parse(" D74.2 "));
        Assert.Equal("D74.2", spec.RawText);
    }
}
