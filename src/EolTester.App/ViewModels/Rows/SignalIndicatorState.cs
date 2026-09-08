using CommunityToolkit.Mvvm.ComponentModel;

namespace EolTester.App.ViewModels.Rows;

/// <summary>Trạng thái 1 tín hiệu bit đơn (LED1/LED2/Phát hiện sản phẩm ở tab Main) đọc từ địa chỉ
/// Dxxxx.Y cấu hình trong spec-register-map.csv — xem PlcPollingService.</summary>
public partial class SignalIndicatorState : ObservableObject
{
    [ObservableProperty]
    private bool _value;
}
