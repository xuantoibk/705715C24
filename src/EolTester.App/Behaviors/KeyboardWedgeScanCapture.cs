using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EolTester.App.ViewModels;
using EolTester.Core.Enums;

namespace EolTester.App.Behaviors;

/// <summary>
/// Đầu đọc barcode USB kiểu "bàn phím ảo" gõ ký tự thẳng vào bất kỳ control nào đang có keyboard focus, như
/// bàn phím thật — WPF không có cách nào phân biệt "sự kiện gõ phím này tới từ scanner hay người dùng thật"
/// ngoại trừ suy luận qua nội dung/tốc độ gõ. Class này hook PreviewTextInput/PreviewKeyDown ở cấp Window
/// (tunnel xuống TRƯỚC khi control đang focus nhận được), cho phép chặn (<c>e.Handled = true</c>) và định
/// tuyến ký tự quét thẳng vào <see cref="ShellViewModel.ScanBarcodeText"/> bất kể đang focus ở đâu.
/// <para>
/// 3 chế độ (<see cref="BarcodeScanMode"/>, đọc LIVE từ <c>ShellViewModel.SettingTab</c> mỗi lần cần quyết
/// định, vì Admin có thể đổi cấu hình giữa phiên):
/// - PrefixStripped/PrefixKept: dò khớp tiền tố ký tự-theo-ký tự — TẠM GIỮ (Handled=true) mọi ký tự ngay từ
///   ký tự đầu tiên khớp <c>prefix[0]</c>, chỉ thực sự bắt đầu hiển thị vào ô Scan sau khi khớp đủ toàn bộ
///   tiền tố → an toàn tuyệt đối, không ký tự nào trong quá trình dò khớp lọt ra control đang focus. Đánh đổi
///   duy nhất: nếu người dùng gõ tay đúng vài ký tự đầu trùng tiền tố rồi dừng/gõ khác, các ký tự đó bị "nuốt"
///   thay vì hiển thị ở ô đang gõ (hiếm gặp nếu chọn tiền tố khác thường, khuyến nghị 1 ký tự để loại hẳn rủi
///   ro này). Kết thúc bằng Enter.
/// - FixedLength: không có ký tự đánh dấu bắt đầu — dùng tốc độ gõ (ký tự cách nhau &lt; ngưỡng) để nhận diện
///   đang trong 1 chuỗi quét liên tục, tự commit ngay khi đủ <c>ScanCodeLength</c> ký tự, KHÔNG cần Enter.
///   Ký tự ĐẦU TIÊN của mỗi chuỗi cố ý KHÔNG bị suppress (không thể biết trước đây có phải bắt đầu 1 lần quét
///   hay không chỉ từ 1 ký tự) — đánh đổi đã thống nhất với người dùng, khác PrefixStripped/PrefixKept.
/// </para>
/// <para>
/// <b>Chỉ hoạt động khi ô "Mã Scan quét được" (<paramref name="scanBox"/> trong <see cref="Attach"/>) đang có
/// keyboard focus.</b> Trước đây class hook toàn Window và ở SCAN MODE khi máy chưa sẵn sàng thì nuốt MỌI ký
/// tự gõ vào bất kỳ ô nào (login, Job, Setup...) — bug thật khiến không gõ được gì trên bàn thử. Giờ nếu focus
/// đang ở ô khác thì class không can thiệp; đánh đổi đã thống nhất: quét trong khi focus ở ô khác có thể để
/// lọt ký tự vào ô đó (FixedLength có thể nhận thừa 1 ký tự → quét lại, xác suất rất thấp).
/// </para>
/// </summary>
public static class KeyboardWedgeScanCapture
{
    public static void Attach(Window window, ShellViewModel viewModel, Control scanBox)
    {
        var state = new CaptureState(viewModel, scanBox);
        window.AddHandler(UIElement.PreviewTextInputEvent, new TextCompositionEventHandler((_, e) => state.OnTextInput(e)), handledEventsToo: true);
        window.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler((_, e) => state.OnKeyDown(e)), handledEventsToo: true);
    }

    private enum Stage { Idle, PendingPrefix, Capturing }

    private sealed class CaptureState(ShellViewModel viewModel, Control scanBox)
    {
        private const int PendingPrefixTimeoutMs = 500;
        private const int FixedLengthBurstThresholdMs = 50;

        private Stage _stage = Stage.Idle;
        private string _pendingPrefix = string.Empty;
        private string _fixedLengthBuffer = string.Empty;
        private DateTime _lastCharAt = DateTime.MinValue;

        private bool ScanBoxHasFocus => scanBox.IsKeyboardFocused;

        public void OnTextInput(TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            // Ô Scan không có focus → không can thiệp gì (để người dùng gõ tay bình thường ở mọi ô khác).
            if (!ScanBoxHasFocus)
            {
                if (_stage != Stage.Idle) ResetToIdle();
                return;
            }

            var settings = viewModel.SettingTab;

            // SCAN MODE (settings.ScanRevMode=false): máy phải đang ở trạng thái chờ (SIGNAL_MACHINE_WAITING=0)
            // và chưa tự gửi CMD_START (D100=0) mới được nhận ký tự quét — chặn TUYỆT ĐỐI (kể cả ký tự đầu
            // FixedLength, vốn cố ý không suppress ở nhánh bình thường, xem HandleFixedLength) khi máy đang
            // bận, không để bất kỳ ký tự nào lọt vào "Mã Scan quét được" hay control khác đang focus. REV MODE
            // (ScanRevMode=true) không bị chặn — giữ nguyên hành vi cũ.
            if (!settings.ScanRevMode && !viewModel.IsScanInputAllowedNow())
            {
                ResetToIdle();
                e.Handled = true;
                return;
            }

            var suppress = settings.ScanMode == BarcodeScanMode.FixedLength
                ? HandleFixedLength(e.Text, settings.ScanCodeLength)
                : HandlePrefixMode(e.Text, settings.ScanMode, settings.ScanPrefixText);

            if (suppress) e.Handled = true;
        }

        private void ResetToIdle()
        {
            _stage = Stage.Idle;
            _pendingPrefix = string.Empty;
            _fixedLengthBuffer = string.Empty;
        }

        public void OnKeyDown(KeyEventArgs e)
        {
            if (!ScanBoxHasFocus)
            {
                if (_stage != Stage.Idle) ResetToIdle();
                return;
            }
            if (_stage != Stage.Capturing) return;

            if (e.Key == Key.Enter)
            {
                viewModel.CommitScanCommand.Execute(null);
                _stage = Stage.Idle;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                viewModel.ScanBarcodeText = string.Empty;
                _stage = Stage.Idle;
                e.Handled = true;
            }
        }

        private bool HandleFixedLength(string text, int targetLength)
        {
            // Cấu hình lỗi (Admin gõ độ dài mã scan &lt; 1 vào ô Set Spec., không có validation ở đó) —
            // không can thiệp, để ký tự đi thẳng vào control đang focus như gõ tay bình thường. Nếu không
            // chặn ở đây, `_fixedLengthBuffer[..targetLength]` bên dưới ném ArgumentOutOfRangeException mỗi
            // phím gõ (bị DispatcherUnhandledException nuốt nhưng spam log).
            if (targetLength < 1) return false;

            var now = DateTime.UtcNow;
            var gapMs = (now - _lastCharAt).TotalMilliseconds;
            _lastCharAt = now;

            var isFirstOfBurst = _fixedLengthBuffer.Length == 0;
            if (!isFirstOfBurst && gapMs > FixedLengthBurstThresholdMs)
            {
                // Khoảng lặng quá lâu giữa chừng — buffer cũ không còn là 1 chuỗi quét liên tục, hủy và coi
                // ký tự này là khởi đầu mới.
                _fixedLengthBuffer = string.Empty;
                isFirstOfBurst = true;
            }

            _fixedLengthBuffer += text;

            if (_fixedLengthBuffer.Length >= targetLength)
            {
                var scanned = _fixedLengthBuffer[..targetLength];
                _fixedLengthBuffer = string.Empty;
                viewModel.ScanBarcodeText = scanned;
                viewModel.CommitScanCommand.Execute(null);
            }

            // Ký tự đầu tiên của mỗi chuỗi cố ý không suppress — xem docstring class.
            return !isFirstOfBurst;
        }

        private bool HandlePrefixMode(string text, BarcodeScanMode mode, string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return false; // Chưa cấu hình tiền tố — không can thiệp gì.

            var now = DateTime.UtcNow;
            if (_stage == Stage.PendingPrefix && (now - _lastCharAt).TotalMilliseconds > PendingPrefixTimeoutMs)
            {
                _stage = Stage.Idle;
                _pendingPrefix = string.Empty;
            }
            _lastCharAt = now;

            switch (_stage)
            {
                case Stage.Idle:
                    if (text.Length != 1 || text[0] != prefix[0]) return false; // Gõ tay bình thường — không đụng gì.
                    _pendingPrefix = text;
                    if (_pendingPrefix.Length == prefix.Length) EnterCapturing(mode, prefix);
                    else _stage = Stage.PendingPrefix;
                    return true;

                case Stage.PendingPrefix:
                    var candidate = _pendingPrefix + text;
                    if (!prefix.StartsWith(candidate, StringComparison.Ordinal))
                    {
                        // Trật khớp — hủy buffer tạm, chấp nhận đánh đổi đã ghi ở docstring (nuốt luôn ký tự
                        // gây trật khớp thay vì cố phát lại vào control đang focus).
                        _stage = Stage.Idle;
                        _pendingPrefix = string.Empty;
                        return true;
                    }
                    _pendingPrefix = candidate;
                    if (_pendingPrefix.Length == prefix.Length) EnterCapturing(mode, prefix);
                    return true;

                case Stage.Capturing:
                    viewModel.ScanBarcodeText += text;
                    return true;

                default:
                    return false;
            }
        }

        private void EnterCapturing(BarcodeScanMode mode, string prefix)
        {
            _stage = Stage.Capturing;
            viewModel.ScanBarcodeText = mode == BarcodeScanMode.PrefixKept ? prefix : string.Empty;
            _pendingPrefix = string.Empty;
        }
    }
}
