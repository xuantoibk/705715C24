using System.Net;
using System.Net.Sockets;
using EolTester.Communication.ModbusRtu;
using EolTester.Core.Enums;
using NModbus;

namespace EolTester.Communication.Tests;


public class ModbusRtuDriverTests
{
    private const byte StationAddress = 1;

    [Fact]
    public async Task ReadRegisterAsync_ReturnsValueFromPlcSlave()
    {
        var registerTable = new PlcRegisterImage();
        registerTable.ConfigureAllowedRanges([(1000, 20)]);
        registerTable.StoreReadBlock(1005, [4242]);

        await using var harness = await DriverTestHarness.StartAsync(registerTable, StationAddress);

        var value = await harness.Driver.ReadRegisterAsync(new IoAddress("1005"), CancellationToken.None);

        Assert.Equal(4242, value);
    }

    [Fact]
    public async Task WriteRegisterAsync_AppliesValueIntoPlcSlave()
    {
        var registerTable = new PlcRegisterImage();
        registerTable.ConfigureAllowedRanges([(1000, 20)]);

        await using var harness = await DriverTestHarness.StartAsync(registerTable, StationAddress);

        await harness.Driver.WriteRegisterAsync(new IoAddress("1006"), 777, CancellationToken.None);

        Assert.True(registerTable.TryGetWord(1006, out var value));
        Assert.Equal((ushort)777, value);
    }

    [Fact]
    public async Task WriteDiscreteAsync_ThenReadDiscreteAsync_RoundTripsThroughPlcSlave()
    {
        var registerTable = new PlcRegisterImage();
        registerTable.ConfigureAllowedRanges([(1000, 20)]);

        await using var harness = await DriverTestHarness.StartAsync(registerTable, StationAddress);

        await harness.Driver.WriteDiscreteAsync(new IoAddress("3"), true, CancellationToken.None);
        var value = await harness.Driver.ReadDiscreteAsync(new IoAddress("3"), CancellationToken.None);

        Assert.True(value);
    }

    [Fact]
    public async Task ReadRegistersAsync_ReturnsMultipleValuesInOneRoundTrip()
    {
        var registerTable = new PlcRegisterImage();
        registerTable.ConfigureAllowedRanges([(1000, 20)]);
        registerTable.StoreReadBlock(1005, [111, 222, 333]);

        await using var harness = await DriverTestHarness.StartAsync(registerTable, StationAddress);

        var values = await harness.Driver.ReadRegistersAsync(new IoAddress("1005"), 3, CancellationToken.None);

        Assert.Equal([111, 222, 333], values);
    }

    [Fact]
    public async Task WriteRegistersAsync_AppliesAllValuesIntoPlcSlave()
    {
        var registerTable = new PlcRegisterImage();
        registerTable.ConfigureAllowedRanges([(1000, 20)]);

        await using var harness = await DriverTestHarness.StartAsync(registerTable, StationAddress);

        await harness.Driver.WriteRegistersAsync(new IoAddress("1010"), [10, 20, 30], CancellationToken.None);

        Assert.True(registerTable.TryGetWord(1010, out var v0));
        Assert.True(registerTable.TryGetWord(1011, out var v1));
        Assert.True(registerTable.TryGetWord(1012, out var v2));
        Assert.Equal((ushort)10, v0);
        Assert.Equal((ushort)20, v1);
        Assert.Equal((ushort)30, v2);
    }

    [Fact]
    public async Task ConnectAsync_WhenPortCannotOpen_SetsStateToError()
    {
        var driver = new ModbusRtuDriver();
        driver.Configure("COM_NON_EXISTENT_PORT_XYZ", 9600, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One, StationAddress, 200, 1);

        await Assert.ThrowsAnyAsync<Exception>(() => driver.ConnectAsync(CancellationToken.None));

        Assert.Equal(ConnectionState.Error, driver.State);
    }

    private sealed class DriverTestHarness : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly TcpClient _plcSideClient;
        private readonly TcpClient _driverSideClient;
        private readonly ModbusSlaveService _plcSlave;

        public ModbusRtuDriver Driver { get; }

        private DriverTestHarness(TcpListener listener, TcpClient plcSideClient, TcpClient driverSideClient, ModbusSlaveService plcSlave, ModbusRtuDriver driver)
        {
            _listener = listener;
            _plcSideClient = plcSideClient;
            _driverSideClient = driverSideClient;
            _plcSlave = plcSlave;
            Driver = driver;
        }

 
        public static async Task<DriverTestHarness> StartAsync(PlcRegisterImage registerTable, byte stationAddress)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var acceptTask = listener.AcceptTcpClientAsync();
            var driverSideClient = new TcpClient();
            await driverSideClient.ConnectAsync(IPAddress.Loopback, port);
            var plcSideClient = await acceptTask;

            var plcStream = new StreamAdapter(plcSideClient.GetStream()) { ReadTimeout = 3000, WriteTimeout = 3000 };
            var plcSlave = new ModbusSlaveService(registerTable);
            await plcSlave.StartWithStreamAsync(plcStream, stationAddress);

            var driverStream = new StreamAdapter(driverSideClient.GetStream()) { ReadTimeout = 3000, WriteTimeout = 3000 };
            var driver = new ModbusRtuDriver();
            driver.Configure("UNUSED", 9600, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One, stationAddress, 3000, 1);
            driver.AttachStream(driverStream);

            return new DriverTestHarness(listener, plcSideClient, driverSideClient, plcSlave, driver);
        }

        public async ValueTask DisposeAsync()
        {
            await Driver.DisposeAsync();
            await _plcSlave.DisposeAsync();
            _driverSideClient.Dispose();
            _plcSideClient.Dispose();
            _listener.Stop();
        }
    }
}
