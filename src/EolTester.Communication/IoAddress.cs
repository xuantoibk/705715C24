namespace EolTester.Communication;

public readonly record struct IoAddress(string Value)
{
    public static implicit operator string(IoAddress address) => address.Value;
    public override string ToString() => Value;
}
