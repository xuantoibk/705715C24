using System.Windows;
using EolTester.App.ViewModels;

namespace EolTester.App;

public partial class LicenseWindow : Window
{
    public LicenseWindow(LicenseViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RegisteredSuccessfully += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
