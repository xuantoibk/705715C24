using EolTester.Communication;
using EolTester.Configuration.Models;

namespace EolTester.App.Services;

/// <summary>Chọn cài đặt <see cref="IPlcSlaveService"/> (vai trò Slave/Adapter) theo <see cref="ProtocolType"/> —
/// tương tự <see cref="IPlcCommunicationDriverFactory"/> nhưng cho chiều Slave.</summary>
public interface IPlcSlaveServiceFactory
{
    IPlcSlaveService GetSlaveService(ProtocolType protocol);
}
