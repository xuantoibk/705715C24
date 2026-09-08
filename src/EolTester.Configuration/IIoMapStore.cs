using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public interface IIoMapStore
{
    Task<IoMapProfile> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(IoMapProfile profile, CancellationToken ct = default);
}
