using Microsoft.Win32;

namespace KeygenApp;

/// <summary>
/// Reads/writes the same HKEY_CURRENT_USER key the AutoLISP tool uses
/// (regdir = "HKEY_CURRENT_USER\SOFTWARE\Autodesk\autolisp\BXT"), so both
/// tools observe and update the same registration state.
/// </summary>
public static class RegistryHelper
{
    public static string? ReadValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(BxtKeygen.RegistryKeyPath, writable: false);
        return key?.GetValue(name) as string;
    }

    public static void WriteValue(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(BxtKeygen.RegistryKeyPath, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }
}
