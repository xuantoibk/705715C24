using EolTester.Core.Models;

namespace EolTester.Configuration;

public interface ITestParametersStore
{
    Task<TestParameters> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(TestParameters parameters, CancellationToken ct = default);
}
