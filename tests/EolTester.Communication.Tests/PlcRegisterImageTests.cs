namespace EolTester.Communication.Tests;

public class PlcRegisterImageTests
{
    private static PlcRegisterImage CreateTableWithInputRange(int start = 1000, int count = 20)
    {
        var table = new PlcRegisterImage();
        table.ConfigureAllowedRanges([(start, count)]);
        return table;
    }

    [Fact]
    public void StoreReadBlock_ThenTryGetWord_ReturnsCorrectValue()
    {
        var table = CreateTableWithInputRange();
        table.StoreReadBlock(1000, [10, 20, 30]);

        Assert.True(table.TryGetWord(1001, out var value));
        Assert.Equal((ushort)20, value);
    }

    [Fact]
    public void StoreReadBlock_ThenTryGetValue_ByDAddressString_ReturnsCorrectValue()
    {
        var table = CreateTableWithInputRange();
        table.StoreReadBlock(1000, [10, 20, 30, 40, 50, 60]);

        Assert.True(table.TryGetValue("D1005", out var value));
        Assert.Equal(60, value);
    }

    [Fact]
    public void TryGetValue_WordAddress_InterpretsAsSignedShort()
    {
        var table = CreateTableWithInputRange();
        table.TryUpdateWord(1005, unchecked((ushort)(short)-6));

        Assert.True(table.TryGetValue("D1005", out var value));
        Assert.Equal(-6, value);
    }

    [Fact]
    public void SetBit_ThenTryGetBit_RoundTripsWithoutAffectingOtherBits()
    {
        var table = CreateTableWithInputRange();
        table.StoreReadBlock(1000, [0]);

        Assert.True(table.SetBit(1000, 3, true));
        Assert.True(table.SetBit(1000, 5, true));

        Assert.True(table.TryGetBit(1000, 3, out var bit3));
        Assert.True(bit3);
        Assert.True(table.TryGetBit(1000, 5, out var bit5));
        Assert.True(bit5);
        Assert.True(table.TryGetBit(1000, 4, out var bit4));
        Assert.False(bit4);

        Assert.True(table.SetBit(1000, 3, false));
        Assert.True(table.TryGetBit(1000, 3, out var bit3After));
        Assert.False(bit3After);
        Assert.True(table.TryGetBit(1000, 5, out var bit5Still));
        Assert.True(bit5Still);
    }

    [Fact]
    public void TryGetValue_BitAddress_MatchesSetBit()
    {
        var table = CreateTableWithInputRange();
        table.StoreReadBlock(1005, [0]);
        table.SetBit(1005, 1, true);

        Assert.True(table.TryGetValue("D1005.1", out var value));
        Assert.Equal(1, value);

        Assert.True(table.TryGetValue("D1005.0", out var otherBit));
        Assert.Equal(0, otherBit);
    }

    [Fact]
    public void TryGetWord_UnwrittenAddressInRange_ReturnsFalse()
    {
        var table = CreateTableWithInputRange();
        Assert.False(table.TryGetWord(1010, out var value));
        Assert.Equal((ushort)0, value);
    }

    [Fact]
    public void TryGetWord_AddressOutsideConfiguredRange_ReturnsFalse()
    {
        var table = new PlcRegisterImage();
        Assert.False(table.TryGetWord(1000, out _));
        Assert.False(table.TryGetValue("D1000", out _));
        Assert.False(table.SetBit(1000, 0, true));
    }

    [Fact]
    public void ConfigureAllowedRanges_Narrowing_ClearsPreviousData()
    {
        var table = CreateTableWithInputRange(1000, 20);
        table.StoreReadBlock(1000, [111]);
        Assert.True(table.TryGetWord(1000, out var before));
        Assert.Equal((ushort)111, before);

        table.ConfigureAllowedRanges([(2000, 10)]);

        Assert.False(table.TryGetWord(1000, out _));
    }

    [Fact]
    public void StoreReadBlock_ExceedingConfiguredRange_SkipsOutOfRangeAddressesWithoutThrowing()
    {
        var table = CreateTableWithInputRange(1000, 5);

        table.StoreReadBlock(1000, [1, 2, 3, 4, 5, 6]);

        Assert.True(table.TryGetWord(1004, out var lastInRange));
        Assert.Equal((ushort)5, lastInRange);
        Assert.False(table.TryGetWord(1005, out _));
    }

    [Fact]
    public void SnapshotBlock_ReturnsValuesInOrder_DefaultsToZeroForUnwritten()
    {
        var table = CreateTableWithInputRange();
        table.TryUpdateWord(1000, 10);
        table.TryUpdateWord(1002, 30);

        var block = table.SnapshotBlock(1000, 3);

        Assert.Equal((ushort)10, block[0]);
        Assert.Equal((ushort)0, block[1]);
        Assert.Equal((ushort)30, block[2]);
    }

    [Fact]
    public void TryUpdateWord_WithinRange_ReturnsTrueAndStoresValue()
    {
        var table = CreateTableWithInputRange();
        Assert.True(table.TryUpdateWord(1005, 999));
        Assert.True(table.TryGetWord(1005, out var value));
        Assert.Equal((ushort)999, value);
    }

    [Fact]
    public void TryUpdateWord_OutsideRange_ReturnsFalseWithoutThrowing()
    {
        var table = CreateTableWithInputRange(1000, 5);
        Assert.False(table.TryUpdateWord(5000, 1));
        Assert.False(table.TryGetWord(5000, out _));
    }

    [Fact]
    public void MasterModeAccessRules_AllowReadOnlyAndWriteOnlyRanges()
    {
        var table = CreateTableWithInputRange(200, 10);

        Assert.True(table.CanReadAddress(0));
        Assert.False(table.CanWriteAddress(0));

        Assert.False(table.CanReadAddress(100));
        Assert.True(table.CanWriteAddress(100));

        Assert.True(table.CanReadAddress(200));
        Assert.True(table.CanWriteAddress(200));
    }

    [Fact]
    public void TryUpdateWord_RespectMasterModeWriteOnlyRange()
    {
        var table = CreateTableWithInputRange(200, 10);

        Assert.False(table.TryUpdateWord(50, 123));
        Assert.True(table.TryUpdateWord(100, 456));
        Assert.True(table.TryGetWord(100, out var value));
        Assert.Equal((ushort)456, value);
    }

    [Fact]
    public void SnapshotAll_ReturnsCopyOfCurrentData()
    {
        var table = CreateTableWithInputRange();
        table.StoreReadBlock(1000, [10, 20, 30]);

        var snapshot = table.SnapshotAll();

        Assert.Equal((ushort)10, snapshot[1000]);
        Assert.Equal((ushort)20, snapshot[1001]);
        Assert.Equal((ushort)30, snapshot[1002]);
        table.TryUpdateWord(1000, 111);
        Assert.Equal((ushort)10, snapshot[1000]);
    }
}
