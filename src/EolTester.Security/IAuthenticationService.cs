using EolTester.Core.Models;

namespace EolTester.Security;

public interface IAuthenticationService
{
    UserAccount? CurrentUser { get; }
    IReadOnlyList<string> AvailableUserNames { get; }
    event EventHandler? CurrentUserChanged;

    bool Login(string userName, string password);
    void Logout();
}
