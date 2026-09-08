using EolTester.Core.Models;

namespace EolTester.Security;

public interface IUserStore
{
    UserAccount? FindByUserName(string userName);
    IReadOnlyList<string> GetUserNames();
}
