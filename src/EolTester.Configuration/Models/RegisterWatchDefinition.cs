namespace EolTester.Configuration.Models;


public enum RegisterDataType
{
    WordSigned,
    WordUnsigned,
    DwordSigned,
    DwordUnsigned,
    Float32
}


public enum RegisterDisplayFormat
{
    Bin,
    Hex,
    Dec,
    Decimal,
    Float,
    Char
}


public sealed class RegisterWatchDefinition
{
    public string Address { get; set; } = string.Empty;
    public RegisterDataType DataType { get; set; } = RegisterDataType.WordUnsigned;
    public RegisterDisplayFormat DisplayFormat { get; set; } = RegisterDisplayFormat.Dec;
    public int Order { get; set; }
}

public sealed class RegisterWatchProfile
{
    public List<RegisterWatchDefinition> Rows { get; set; } = [];
}
