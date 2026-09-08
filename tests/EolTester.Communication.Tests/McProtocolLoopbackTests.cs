using EolTester.Communication.Mc;

namespace EolTester.Communication.Tests;

public class McProtocolLoopbackTests
{
    [Theory]
    [InlineData(McFrameFormatKind.Binary)]
    [InlineData(McFrameFormatKind.Ascii)]
    public async Task Master_ReadsWordFromSlave_OverTcpLoopback(McFrameFormatKind frameFormat)
    {
        var registerTable = new PlcRegisterImage();
        registerTable.ConfigureAllowedRanges([(1000, 20)]);
        registerTable.StoreReadBlock(1005, [4242]);

        await using var slave = new McProtocolSlaveService(registerTable);
        slave.Configure("127.0.0.1", 0, frameFormat);
        await slave.StartAsync();

        await using var driver = new McProtocolDriver();
        driver.Configure("127.0.0.1", slave.ListeningPort!.Value, timeoutMs: 3000, retryCount: 0, frameFormat);

        var readTask = driver.ReadRegisterAsync(new IoAddress("1005"), CancellationToken.None);
        var completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == readTask, "Timed out waiting for MC Protocol read response.");

        Assert.Equal(4242, await readTask);
    }

    [Theory]
    [InlineData(McFrameFormatKind.Binary)]
    [InlineData(McFrameFormatKind.Ascii)]
    public async Task Master_WritesWordToSlave_OverTcpLoopback(McFrameFormatKind frameFormat)
    {
        var registerTable = new PlcRegisterImage();
        registerTable.ConfigureAllowedRanges([(1000, 20)]);

        await using var slave = new McProtocolSlaveService(registerTable);
        slave.Configure("127.0.0.1", 0, frameFormat);
        await slave.StartAsync();

        await using var driver = new McProtocolDriver();
        driver.Configure("127.0.0.1", slave.ListeningPort!.Value, timeoutMs: 3000, retryCount: 0, frameFormat);

        var writeTask = driver.WriteRegisterAsync(new IoAddress("1006"), 777, CancellationToken.None);
        var completed = await Task.WhenAny(writeTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == writeTask, "Timed out waiting for MC Protocol write response.");
        await writeTask;

        Assert.True(registerTable.TryGetWord(1006, out var value));
        Assert.Equal((ushort)777, value);
    }
}
