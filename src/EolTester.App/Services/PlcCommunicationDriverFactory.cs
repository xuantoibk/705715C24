using EolTester.Communication;
using EolTester.Communication.Mc;
using EolTester.Communication.ModbusRtu;
using EolTester.Communication.Slmp;
using EolTester.Configuration.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EolTester.App.Services;

public sealed class PlcCommunicationDriverFactory(IServiceProvider services) : IPlcCommunicationDriverFactory
{
    public IPlcCommunicationDriver GetDriver(ProtocolType protocol) => protocol switch
    {
        ProtocolType.ModbusRtu => services.GetRequiredService<ModbusRtuDriver>(),
        ProtocolType.McProtocol => services.GetRequiredService<McProtocolDriver>(),
        ProtocolType.Slmp => services.GetRequiredService<SlmpDriver>(),
        _ => throw new NotSupportedException($"Giao thức {protocol} chưa được hỗ trợ ở vai trò Master."),
    };
}
