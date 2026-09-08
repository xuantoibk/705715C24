using EolTester.Core.Enums;

namespace EolTester.Core.Models;

public sealed class TestParameters
{
    public int ScanCodeLength { get; set; } = 12;

    // Đơn vị các thanh ghi thời gian bên dưới là x0.1s (PLC), KHÔNG phải ms — nhãn UI "Cài đặt thời gian (x0.1s)".
    // Mặc định: High/Low = 20 (2.0s), Button hold = 5 (0.5s). Tên field giữ hậu tố "Ms" vì đổi tên lan rộng
    // nhiều nơi (khóa PLC PARAM_*_MS, resx...) — chỉ đơn vị/nhãn đổi.
    public int HighModeDurationMs { get; set; } = 20;
    public int LowModeDurationMs { get; set; } = 20;
    public int ButtonHoldMs { get; set; } = 5;
    public int SerialStartIndex { get; set; } = 1;
    public int SerialEndIndex { get; set; } = 8;
    /// <summary>PARAM_EDIT_SCAN (D104.0, spec-register-map.csv) — false: ô "Mã Scan quét được" chỉ nhận giá
    /// trị từ đầu đọc (không cho gõ tay/sửa thủ công), commit khi hoàn thành 1 lần quét. true: cho phép gõ tay
    /// sửa trực tiếp, commit khi hoàn thành quét hoặc khi nhấn Enter. Xem ShellView (MainWindow.xaml).</summary>
    public bool AllowEditBarcode { get; set; }

    /// <summary>PARAM_REQUIRE_SCAN_ORDER (D104.1, spec-register-map.csv) — SCAN MODE: bật thì ShellViewModel
    /// .CommitScanAsync kiểm tra số thứ tự serial của mã vừa quét phải bằng lần quét hợp lệ trước + 1. Từ
    /// 2026-08-28 là option ĐỘC LẬP: SCAN MODE không còn tự bật/khóa cờ này.</summary>
    public bool RequireScanOrder { get; set; }

    /// <summary>PARAM_CHECK_DUPLICATE_LOG (D104.5, spec-register-map.csv) — SCAN MODE: bật thì kiểm tra mã
    /// vừa quét đã có trong scanlog chống trùng của ngày hôm nay chưa (IBarcodeScanLogService). Option ĐỘC
    /// LẬP với <see cref="RequireScanOrder"/> — SCAN MODE không ép/khóa. Mặc định true để giữ nguyên hành vi
    /// chống trùng vốn luôn chạy trước đây.</summary>
    public bool CheckDuplicateInLog { get; set; } = true;

    /// <summary>Cách nhận diện điểm bắt đầu/kết thúc 1 lần quét barcode — xem CLAUDE.md mục "Đầu đọc barcode".</summary>
    public BarcodeScanMode ScanMode { get; set; } = BarcodeScanMode.PrefixStripped;

    /// <summary>Ký tự/cụm ký tự tiền tố do đầu đọc gửi trước mỗi mã — chỉ có ý nghĩa với
    /// ScanMode=PrefixStripped/PrefixKept. Khuyến nghị 1 ký tự để đảm bảo không lọt ký tự nào ra ô đang focus.</summary>
    public string ScanPrefixText { get; set; } = "`";

    /// <summary>10 giá trị thời gian delay tùy chỉnh (đơn vị x0.1s như các timer khác, không phải ms), đẩy xuống
    /// PARAM_DELAY_TIMER_1..10 (D150-D159, spec-register-map.csv) — tên hiển thị đọc từ cột Label1/Label2 của
    /// cùng file CSV, xem SettingTabViewModel.TimerSettingRows. Mặc định 5 mỗi ô (không phải 0 — tránh giá trị
    /// mờ nghĩa "chưa cấu hình" giống các thanh ghi giới hạn khác, xem CLAUDE.md Phần I mục 3 "Giá trị 0 gây mơ
    /// hồ"); áp dụng cho cả lần khởi tạo đầu tiên (chưa có test-parameters.json) lẫn khi nạp lại 1 ô đang là 0
    /// (xem SettingTabViewModel.LoadAsync).</summary>
    public int[] DelayTimersMs { get; set; } = Enumerable.Repeat(5, 10).ToArray();

    /// <summary>Thứ tự chạy test High/Low mode — PARAM_TEST_ORDER (D104.3, spec-register-map.csv):
    /// false = High -> Low (0), true = Low -> High (1). Khối "MODE TEST" ở tab Set Spec.</summary>
    public bool TestOrderLowToHigh { get; set; }

    /// <summary>PARAM_SCAN_REV_MODE (D104.4, spec-register-map.csv) — false: SCAN MODE (mặc định), ép
    /// <see cref="AllowEditBarcode"/>=false, luồng quét barcode ở footer tự chạy pipeline kiểm tra rồi tự gửi
    /// CMD_START (xem ShellViewModel.CommitScanAsync). <see cref="RequireScanOrder"/> và
    /// <see cref="CheckDuplicateInLog"/> KHÔNG còn bị ép/khóa — là 2 option độc lập, chỉ được đọc khi pipeline
    /// SCAN MODE chạy. true: REV MODE, quay lại hành vi thủ công hoàn toàn (không ép giá trị, không tự động gì).</summary>
    public bool ScanRevMode { get; set; }
}
