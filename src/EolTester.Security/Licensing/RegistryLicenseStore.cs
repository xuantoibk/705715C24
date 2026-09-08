using Microsoft.Win32;

namespace EolTester.Security.Licensing;

/// <summary>
/// Lưu Registration Code/Key dưới HKEY_CURRENT_USER, riêng theo từng sản phẩm (registryKeyPath),
/// tách biệt với registry key của công cụ keygen nội bộ (docs/KeygenApp dùng nhánh Autodesk/BXT
/// của chính nó) — mỗi sản phẩm có nhánh registry riêng dù dùng chung thuật toán.
/// </summary>
public sealed class RegistryLicenseStore : ILicenseStore
{
    public const string DefaultRegistryKeyPath = @"SOFTWARE\TTI\EolTester705715\License";

    private readonly string _registryKeyPath;

    public RegistryLicenseStore(string? registryKeyPath = null)
    {
        _registryKeyPath = registryKeyPath ?? DefaultRegistryKeyPath;
    }

    public string? ReadRegistrationCode() => ReadValue("serial");
    public void WriteRegistrationCode(string code) => WriteValue("serial", code);

    public string? ReadRegistrationKey() => ReadValue("registration");
    public void WriteRegistrationKey(string key) => WriteValue("registration", key);

    private string? ReadValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(_registryKeyPath, writable: false);
        return key?.GetValue(name) as string;
    }

    private void WriteValue(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_registryKeyPath, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }
}
