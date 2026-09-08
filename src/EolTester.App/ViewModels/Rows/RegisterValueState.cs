using CommunityToolkit.Mvvm.ComponentModel;

namespace EolTester.App.ViewModels.Rows;

/// <summary>Giá trị nguyên (word) đọc từ 1 thanh ghi đơn — dùng cho các chỉ báo có nhiều hơn 2 trạng thái
/// (VD "Phát hiện Hàng NG": 1/2/khác), khác <see cref="SignalIndicatorState"/> vốn chỉ có 2 trạng thái bit.</summary>
public partial class RegisterValueState : ObservableObject
{
    [ObservableProperty]
    private int? _value;
}
