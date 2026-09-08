using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using EolTester.App.Behaviors;
using EolTester.App.Interop;
using EolTester.App.ViewModels;

namespace EolTester.App;

public partial class MainWindow : Window
{
    private const double DesignWidth = 1400;
    private const double DesignHeight = 765;
    private const double MinDesignWidth = 1280;

    private readonly ShellViewModel _viewModel;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        var workArea = SystemParameters.WorkArea;
        var width = Math.Max(workArea.Width * 0.8, MinDesignWidth);
        Width = width;
        Height = width * (DesignHeight / DesignWidth);

        WindowAspectRatioLock.Attach(this, DesignWidth / DesignHeight);
        KeyboardWedgeScanCapture.Attach(this, _viewModel, ScanInputBox);

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>PasswordBox không bind được nên phải xóa tay — ViewModel tự đặt <c>LoginPassword=""</c> sau khi
    /// đăng nhập thành công, và ô nhập lại hiện ra khi đăng xuất; nếu không xóa, mật khẩu cũ vẫn nằm trong ô
    /// (khuất khi đã đăng nhập, lộ lại khi đăng xuất). Chỉ xóa khi thực sự lệch — set .Password="" lại kích
    /// hoạt PasswordChanged nhưng gán chuỗi rỗng đã rỗng nên không tạo vòng lặp.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.LoginPassword) or nameof(ShellViewModel.IsLoggedIn)
            && string.IsNullOrEmpty(_viewModel.LoginPassword)
            && LoginPasswordBox.Password.Length > 0)
        {
            LoginPasswordBox.Clear();
        }
    }

    private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.LoginPassword = ((PasswordBox)sender).Password;
    }
}
