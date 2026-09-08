using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EolTester.App.Localization;
using EolTester.Security.Licensing;

namespace EolTester.App.ViewModels;

public partial class LicenseViewModel : ObservableObject
{
    private readonly ILicenseService _licenseService;

    [ObservableProperty] private string _registrationCode = string.Empty;
    [ObservableProperty] private string _enteredKey = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public event EventHandler? RegisteredSuccessfully;

    public LicenseViewModel(ILicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    public void LoadCurrentCode(string registrationCode)
    {
        RegistrationCode = registrationCode;
    }

    [RelayCommand]
    private void Register()
    {
        if (_licenseService.TryRegister(EnteredKey))
        {
            StatusMessage = Translation.Instance["License_Success"];
            RegisteredSuccessfully?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            StatusMessage = Translation.Instance["License_InvalidKey"];
        }
    }
}
