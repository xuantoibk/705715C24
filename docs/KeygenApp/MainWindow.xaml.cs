using System.Windows;

namespace KeygenApp;

public partial class MainWindow : Window
{
    private string _localHddHex = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshAll();
    }

    /// <summary>Equivalent of BXT_checkkey: ensure a registration code exists for this
    /// machine's C: drive (regenerating it if the stored one belongs to a different
    /// drive), then report whether it's already registered.</summary>
    private void RefreshAll()
    {
        try
        {
            _localHddHex = BxtKeygen.GetHddSerialHex("C");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Không đọc được số serial ổ đĩa C: {ex.Message}";
            return;
        }

        string? code = RegistryHelper.ReadValue("serial");
        bool needNewCode = string.IsNullOrEmpty(code);

        if (!needNewCode && BxtKeygen.ValidateCodeFormat(code!))
        {
            string unmapped = BxtKeygen.UnmapSerial(code!, BxtKeygen.HmIdx);
            string storedHdd = unmapped.Length >= 8 ? unmapped.Substring(0, 8) : "";
            if (storedHdd != _localHddHex) needNewCode = true;
        }
        else if (!needNewCode)
        {
            needNewCode = true; // corrupt/invalid stored code
        }

        if (needNewCode)
        {
            code = BxtKeygen.CreateRegistrationCode(_localHddHex, BxtKeygen.HmIdx);
            RegistryHelper.WriteValue("serial", code);
        }

        RegCodeBox.Text = code;
        UpdateStatus(code!);
    }

    private void UpdateStatus(string code)
    {
        var (expectedKey, _) = BxtKeygen.ComputeKeyFromCode(code, BxtKeygen.HmIdx);
        string? storedKey = RegistryHelper.ReadValue("registration");

        if (storedKey != null && storedKey.Equals(expectedKey, StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "Đã đăng ký (Registered).";
        }
        else
        {
            StatusText.Text = "Chưa đăng ký. Gửi \"Mã đăng ký\" ở tab bên dưới cho nhà cung cấp để lấy Key.";
        }
    }

    private void RefreshCode_Click(object sender, RoutedEventArgs e)
    {
        string code = BxtKeygen.CreateRegistrationCode(_localHddHex, BxtKeygen.HmIdx);
        RegistryHelper.WriteValue("serial", code);
        RegCodeBox.Text = code;
        UpdateStatus(code);
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(RegCodeBox.Text))
            Clipboard.SetText(RegCodeBox.Text);
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        string code = RegCodeBox.Text;
        string enteredKey = (RegKeyBox.Text ?? "").Trim().ToUpperInvariant();

        if (!BxtKeygen.ValidateCodeFormat(enteredKey))
        {
            MessageBox.Show(this, "Please check your Key!", "BXT Keygen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (expectedKey, _) = BxtKeygen.ComputeKeyFromCode(code, BxtKeygen.HmIdx);
        if (enteredKey == expectedKey)
        {
            RegistryHelper.WriteValue("registration", enteredKey);
            MessageBox.Show(this, "Your registration was successful!", "BXT Keygen", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateStatus(code);
        }
        else
        {
            MessageBox.Show(this, "Your registration was unsuccessful!", "BXT Keygen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UseLocalCode_Click(object sender, RoutedEventArgs e)
    {
        GenCodeBox.Text = RegCodeBox.Text;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        string code = (GenCodeBox.Text ?? "").Trim().ToUpperInvariant();
        RegisterFromGenBtn.IsEnabled = false;
        GenKeyBox.Text = "";

        if (!BxtKeygen.ValidateCodeFormat(code))
        {
            MessageBox.Show(this, "Please check your Key!", "BXT Keygen", MessageBoxButton.OK, MessageBoxImage.Warning);
            GenHintText.Text = "";
            return;
        }

        var (key, embeddedHddHex) = BxtKeygen.ComputeKeyFromCode(code, BxtKeygen.HmIdx);
        GenKeyBox.Text = key;

        bool matchesThisMachine = embeddedHddHex == _localHddHex;
        RegisterFromGenBtn.IsEnabled = matchesThisMachine;
        GenHintText.Text = matchesThisMachine
            ? "Mã này thuộc về máy hiện tại — có thể đăng ký trực tiếp."
            : "Mã này không thuộc máy hiện tại. Hãy gửi Key ở trên cho người dùng của mã đó để họ tự đăng ký.";
    }

    private void RegisterFromGen_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(GenKeyBox.Text)) return;
        RegistryHelper.WriteValue("registration", GenKeyBox.Text);
        MessageBox.Show(this, "Your registration was successful!", "BXT Keygen", MessageBoxButton.OK, MessageBoxImage.Information);
        RefreshAll();
    }
}
