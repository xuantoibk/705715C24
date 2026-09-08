using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace EolTester.App.Behaviors;

/// <summary>
/// Biến 1 Button thành nút "giữ = ON, thả = OFF" (kiểu jog vật lý) thay vì Click thường — dùng cho các lệnh
/// điều khiển PLC cần phản ánh đúng thời gian giữ thực tế (ON/OFF thủ công, Reset, Xác nhận NG).
/// LostMouseCapture (không phải MouseUp trực tiếp) đảm bảo ReleaseCommand luôn chạy kể cả khi người dùng
/// kéo chuột ra ngoài nút rồi thả, hoặc mất capture bất thường — tránh bit bị "kẹt" ở ON.
/// </summary>
public static class MomentaryButton
{
    public static readonly DependencyProperty PressCommandProperty =
        DependencyProperty.RegisterAttached("PressCommand", typeof(ICommand), typeof(MomentaryButton),
            new PropertyMetadata(null, OnCommandsChanged));
    public static readonly DependencyProperty ReleaseCommandProperty =
        DependencyProperty.RegisterAttached("ReleaseCommand", typeof(ICommand), typeof(MomentaryButton),
            new PropertyMetadata(null, OnCommandsChanged));
    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached("CommandParameter", typeof(object), typeof(MomentaryButton));

    public static void SetPressCommand(DependencyObject obj, ICommand value) => obj.SetValue(PressCommandProperty, value);
    public static ICommand GetPressCommand(DependencyObject obj) => (ICommand)obj.GetValue(PressCommandProperty);
    public static void SetReleaseCommand(DependencyObject obj, ICommand value) => obj.SetValue(ReleaseCommandProperty, value);
    public static ICommand GetReleaseCommand(DependencyObject obj) => (ICommand)obj.GetValue(ReleaseCommandProperty);
    public static void SetCommandParameter(DependencyObject obj, object value) => obj.SetValue(CommandParameterProperty, value);
    public static object GetCommandParameter(DependencyObject obj) => obj.GetValue(CommandParameterProperty);

    private static void OnCommandsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase button) return;
        button.PreviewMouseLeftButtonDown -= OnPressed;
        button.PreviewMouseLeftButtonDown += OnPressed;
        button.PreviewMouseLeftButtonUp -= OnMouseUp;
        button.PreviewMouseLeftButtonUp += OnMouseUp;
        button.LostMouseCapture -= OnReleased;
        button.LostMouseCapture += OnReleased;
    }

    private static void OnPressed(object sender, MouseButtonEventArgs e)
    {
        var button = (ButtonBase)sender;
        button.CaptureMouse();
        var cmd = GetPressCommand(button);
        var param = GetCommandParameter(button);
        if (cmd?.CanExecute(param) == true) cmd.Execute(param);
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        ((ButtonBase)sender).ReleaseMouseCapture();
    }

    private static void OnReleased(object sender, MouseEventArgs e)
    {
        var button = (ButtonBase)sender;
        var cmd = GetReleaseCommand(button);
        if (cmd is null) return;
        var param = GetCommandParameter(button);
        if (cmd.CanExecute(param)) cmd.Execute(param);
    }
}
