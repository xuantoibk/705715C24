using System.IO.Ports;
using EolTester.Communication;
using EolTester.Communication.ModbusRtu;
using EolTester.Configuration;
using EolTester.Configuration.Models;

var configStore = new JsonConnectionSettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EolTester", "Config"));
var settings = await configStore.LoadAsync();
var cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

Parity ToSerialPortParity(ModbusParity parity) => parity switch
{
    ModbusParity.Odd => Parity.Odd,
    ModbusParity.Even => Parity.Even,
    _ => Parity.None,
};

StopBits ToSerialPortStopBits(int stopBits) => stopBits switch
{
    2 => StopBits.Two,
    _ => StopBits.One,
};

Console.WriteLine($"Using COM: {settings.ComPort}");
Console.WriteLine($"Baud: {settings.BaudRate}, Parity: {settings.Parity}, DataBits: {settings.DataBits}, StopBits: {settings.StopBits}");
Console.WriteLine($"StationAddress: {settings.StationAddress}, Timeout: {settings.TimeoutMs} ms");

var driver = new ModbusRtuDriver();
driver.Configure(
    settings.ComPort,
    settings.BaudRate,
    ToSerialPortParity(settings.Parity),
    settings.DataBits,
    ToSerialPortStopBits(settings.StopBits),
    (byte)settings.StationAddress,
    settings.TimeoutMs,
    settings.RetryCount);

try
{
    await driver.ConnectAsync(CancellationToken.None);
    Console.WriteLine("Connected attempt completed.");

    if (cliArgs.Length == 2 && int.TryParse(cliArgs[0], out var writeAddress) && int.TryParse(cliArgs[1], out var writeValue))
    {
        await driver.WriteRegisterAsync(new IoAddress(writeAddress.ToString()), writeValue, CancellationToken.None);
        Console.WriteLine($"WROTE D{writeAddress} = {writeValue}");
    }
    else
    {
        foreach (var address in new[] { "1000", "1001", "1002" })
        {
            try
            {
                var value = await driver.ReadRegisterAsync(new IoAddress(address), CancellationToken.None);
                Console.WriteLine($"D{address} = {value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"D{address} ERROR: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Connect error: {ex.GetType().Name}: {ex.Message}");
}
finally
{
    await driver.DisposeAsync();
}
