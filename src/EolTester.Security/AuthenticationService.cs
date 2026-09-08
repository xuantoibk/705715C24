using EolTester.Core.Models;

namespace EolTester.Security;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUserStore _userStore;

    public AuthenticationService(IUserStore userStore)
    {
        _userStore = userStore;
    }

    public UserAccount? CurrentUser { get; private set; }
    public IReadOnlyList<string> AvailableUserNames => _userStore.GetUserNames();
    public event EventHandler? CurrentUserChanged;

    public bool Login(string userName, string password)
    {
        var user = _userStore.FindByUserName(userName);
        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            return false;
        }

        CurrentUser = user;
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Logout()
    {
        CurrentUser = null;
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
    }
}
