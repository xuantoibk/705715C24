using EolTester.Configuration.Models;

namespace EolTester.Configuration;

public interface IRegisterWatchStore
{
    Task<RegisterWatchProfile> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(RegisterWatchProfile profile, CancellationToken ct = default);
}
