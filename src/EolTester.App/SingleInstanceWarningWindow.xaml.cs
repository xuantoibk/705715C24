using System.Windows;

namespace EolTester.App;

public partial class SingleInstanceWarningWindow : Window
{
    public SingleInstanceWarningWindow()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}
