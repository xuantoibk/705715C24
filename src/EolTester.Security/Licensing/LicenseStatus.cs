namespace EolTester.Security.Licensing;

public sealed record LicenseStatus(bool IsLicensed, string RegistrationCode, string? ErrorMessage);
