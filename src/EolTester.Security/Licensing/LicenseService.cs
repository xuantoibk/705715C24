namespace EolTester.Security.Licensing;

public sealed class LicenseService : ILicenseService
{
    private readonly ILicenseStore _store;
    private readonly string _driveLetter;

    public LicenseService(ILicenseStore store, string driveLetter = "C")
    {
        _store = store;
        _driveLetter = driveLetter;
    }

    public LicenseStatus CheckLicense()
    {
        string localHddHex;
        try
        {
            localHddHex = LicenseKeyAlgorithm.GetHddSerialHex(_driveLetter);
        }
        catch (Exception ex)
        {
            return new LicenseStatus(false, "", $"Không đọc được số serial ổ đĩa {_driveLetter}: {ex.Message}");
        }

        string? code = _store.ReadRegistrationCode();
        bool needNewCode = string.IsNullOrEmpty(code) || !LicenseKeyAlgorithm.ValidateCodeFormat(code);

        if (!needNewCode)
        {
            string unmapped = LicenseKeyAlgorithm.UnmapSerial(code!, LicenseKeyAlgorithm.HmIdx);
            string storedHdd = unmapped.Length >= 8 ? unmapped.Substring(0, 8) : "";
            if (storedHdd != localHddHex) needNewCode = true;
        }

        if (needNewCode)
        {
            code = LicenseKeyAlgorithm.CreateRegistrationCode(localHddHex, LicenseKeyAlgorithm.HmIdx);
            _store.WriteRegistrationCode(code);
        }

        var (expectedKey, _) = LicenseKeyAlgorithm.ComputeKeyFromCode(code!, LicenseKeyAlgorithm.HmIdx);
        string? storedKey = _store.ReadRegistrationKey();
        bool isLicensed = storedKey is not null && storedKey.Equals(expectedKey, StringComparison.OrdinalIgnoreCase);

        return new LicenseStatus(isLicensed, code!, null);
    }

    public bool TryRegister(string enteredKey)
    {
        string? code = _store.ReadRegistrationCode();
        if (string.IsNullOrEmpty(code)) return false;

        string normalizedKey = (enteredKey ?? "").Trim().ToUpperInvariant();
        if (!LicenseKeyAlgorithm.ValidateCodeFormat(normalizedKey)) return false;

        var (expectedKey, _) = LicenseKeyAlgorithm.ComputeKeyFromCode(code, LicenseKeyAlgorithm.HmIdx);
        if (normalizedKey != expectedKey) return false;

        _store.WriteRegistrationKey(normalizedKey);
        return true;
    }
}
