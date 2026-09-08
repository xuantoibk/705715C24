using EolTester.Communication;
using EolTester.Communication.Mc;
using EolTester.Communication.Slmp;
using EolTester.Configuration.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EolTester.App.Services;

public sealed class PlcSlaveServiceFactory(IServiceProvider services) : IPlcSlaveServiceFactory
{
    public IPlcSlaveService GetSlaveService(ProtocolType protocol) => protocol switch
    {
        ProtocolType.ModbusRtu => services.GetRequiredService<ModbusSlaveService>(),
        ProtocolType.McProtocol => services.GetRequiredService<McProtocolSlaveService>(),
        ProtocolType.Slmp => services.GetRequiredService<SlmpSlaveService>(),
        _ => throw new NotSupportedException($"Giao thức {protocol} chưa được hỗ trợ ở vai trò Slave."),
    };
}
