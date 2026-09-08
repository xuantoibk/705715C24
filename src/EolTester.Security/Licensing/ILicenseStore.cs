namespace EolTester.Security.Licensing;

public interface ILicenseStore
{
    string? ReadRegistrationCode();
    void WriteRegistrationCode(string code);

    string? ReadRegistrationKey();
    void WriteRegistrationKey(string key);
}
