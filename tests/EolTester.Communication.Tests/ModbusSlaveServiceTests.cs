using System.Net;
using System.Net.Sockets;
using NModbus;

namespace EolTester.Communication.Tests;


public class ModbusSlaveServiceTests
{
    [Fact]
    public async Task Slave_RespondsToMasterReadHoldingRegisters_WithValueFromRegisterTable()
    {
        const byte slaveId = 2;
        var registerTable = new PlcRegisterImage();
        registerTable.ConfigureAllowedRanges([(1000, 20)]);
        registerTable.StoreReadBlock(1005, [4242]);

        await using var harness = await SlaveTestHarness.StartAsync(registerTable, slaveId);

        var readTask = harness.Master.ReadHoldingRegistersAsync(slaveId, 1005, 1);
        var completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == readTask, "Timed out waiting for master ReadHoldingRegistersAsync response.");

        var result = await readTask;
        Assert.Equal((ushort)4242, result[0]);
    }

    [Fact]
    public async Task Slave_AppliesMasterWriteSingleRegister_IntoRegisterTable()
    {
        const byte slaveId = 2;
        var registerTable = new PlcRegisterImage();
        registerTable.ConfigureAllowedRanges([(1000, 20)]);

        await using var harness = await SlaveTestHarness.StartAsync(registerTable, slaveId);

        var writeTask = harness.Master.WriteSingleRegisterAsync(slaveId, 1006, 777);
        var completed = await Task.WhenAny(writeTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == writeTask, "Timed out waiting for master WriteSingleRegisterAsync response.");
        await writeTask;

        Assert.True(registerTable.TryGetWord(1006, out var value));
        Assert.Equal((ushort)777, value);
    }

    private sealed class SlaveTestHarness : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly TcpClient _serverSideClient;
        private readonly TcpClient _masterClient;
        private readonly ModbusSlaveService _slaveService;

        public IModbusMaster Master { get; }

        private SlaveTestHarness(TcpListener listener, TcpClient serverSideClient, TcpClient masterClient, ModbusSlaveService slaveService, IModbusMaster master)
        {
            _listener = listener;
            _serverSideClient = serverSideClient;
            _masterClient = masterClient;
            _slaveService = slaveService;
            Master = master;
        }

        public static async Task<SlaveTestHarness> StartAsync(PlcRegisterImage registerTable, byte slaveId)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var acceptTask = listener.AcceptTcpClientAsync();
            var masterClient = new TcpClient();
            await masterClient.ConnectAsync(IPAddress.Loopback, port);
            var serverSideClient = await acceptTask;

            var serverStream = new StreamAdapter(serverSideClient.GetStream()) { ReadTimeout = 3000, WriteTimeout = 3000 };
            var slaveService = new ModbusSlaveService(registerTable);
            await slaveService.StartWithStreamAsync(serverStream, slaveId);

            var masterStream = new StreamAdapter(masterClient.GetStream()) { ReadTimeout = 3000, WriteTimeout = 3000 };
            var factory = new ModbusFactory();
            var master = factory.CreateRtuMaster(masterStream);

            return new SlaveTestHarness(listener, serverSideClient, masterClient, slaveService, master);
        }

        public async ValueTask DisposeAsync()
        {
            await _slaveService.DisposeAsync();
            _masterClient.Dispose();
            _serverSideClient.Dispose();
            _listener.Stop();
        }
    }
}
