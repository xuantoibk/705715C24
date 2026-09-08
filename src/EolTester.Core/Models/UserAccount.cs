using EolTester.Core.Enums;

namespace EolTester.Core.Models;

public sealed class UserAccount
{
    public required string UserName { get; init; }
    public required string PasswordHash { get; init; }
    public required string PasswordSalt { get; init; }
    public required UserRole Role { get; init; }
}
