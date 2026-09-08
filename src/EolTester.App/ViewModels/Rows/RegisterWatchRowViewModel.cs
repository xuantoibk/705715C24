using CommunityToolkit.Mvvm.ComponentModel;
using EolTester.Configuration.Models;

namespace EolTester.App.ViewModels.Rows;

/// <summary>
/// 1 ô cố định trong lưới "Giám sát DATA" ở tab Setup (24 ô cố định — xem SetupTabViewModel). Người dùng
/// tự gõ địa chỉ Dxxxx muốn theo dõi trực tiếp vào ô, và có thể sửa <see cref="CurrentValueDisplay"/> tại
/// chỗ để ghi giá trị mới xuống thanh ghi — xem cơ chế phân biệt "timer tự cập nhật" vs "người dùng vừa
/// gõ xong" ở <see cref="SetDisplayFromPoll"/>/<see cref="ValueEditedByUser"/>.
/// </summary>
public partial class RegisterWatchRowViewModel : ObservableObject
{
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private RegisterDataType _dataType = RegisterDataType.WordUnsigned;
    [ObservableProperty] private RegisterDisplayFormat _displayFormat = RegisterDisplayFormat.Dec;
    [ObservableProperty] private string _currentValueDisplay = string.Empty;
    [ObservableProperty] private string? _validationMessage;

    private bool _suppressEditNotification;

    /// <summary>Raised khi người dùng gõ xong (Enter/rời ô) vào cột Giá trị — KHÔNG raise khi timer tự cập nhật.</summary>
    public event EventHandler? ValueEditedByUser;

    partial void OnCurrentValueDisplayChanged(string value)
    {
        if (!_suppressEditNotification) ValueEditedByUser?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Timer polling gọi hàm này để cập nhật hiển thị — không coi là người dùng chỉnh sửa.</summary>
    public void SetDisplayFromPoll(string text)
    {
        _suppressEditNotification = true;
        CurrentValueDisplay = text;
        _suppressEditNotification = false;
    }

    /// <summary>Áp dữ liệu đã lưu vào ô cố định có sẵn (không tạo instance mới) — không đụng CurrentValueDisplay.</summary>
    public void ApplyDefinition(RegisterWatchDefinition definition)
    {
        Address = definition.Address;
        DataType = definition.DataType;
        DisplayFormat = definition.DisplayFormat;
    }

    public RegisterWatchDefinition ToDefinition(int order) => new()
    {
        Address = Address,
        DataType = DataType,
        DisplayFormat = DisplayFormat,
        Order = order,
    };
}
