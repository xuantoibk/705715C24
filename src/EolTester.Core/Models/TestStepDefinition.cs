using EolTester.Core.Enums;

namespace EolTester.Core.Models;

public sealed class TestStepDefinition
{
    public required string Key { get; init; }
    public required TestMode Mode { get; init; }
    public required int Order { get; init; }
    public required string Description { get; set; }
    public required string Unit { get; set; }
    /// <summary>Giá trị THỰC (đã quy đổi theo <see cref="Scale"/>) mà Admin/Operator nhập/thấy — vd 23.50 (V).
    /// Khi ghi/đọc thanh ghi PLC (WordSigned, -32768..32767), quy đổi 2 chiều raw = value*Scale / value =
    /// raw/Scale — xem PlcPollingService.WriteStepLimitsAsync/ReadMeasurement.</summary>
    public decimal? LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }

    /// <summary>Hệ số Gain quy đổi giá trị thực &lt;-&gt; số nguyên thanh ghi PLC — chỉ nhận 1/10/100/1000
    /// (xem <see cref="EolTester.Core.PlcGainScale"/>), đọc từ cột "Scale" của spec-register-map.csv (khóa
    /// trùng <see cref="Key"/>), không đọc được/không hợp lệ thì mặc định 1 (không scale). Dùng chung cho
    /// <see cref="Address"/>, <see cref="LowerLimitAddress"/>, <see cref="UpperLimitAddress"/> vì cả 3 cùng
    /// biểu diễn 1 đại lượng vật lý duy nhất.</summary>
    public int Scale { get; set; } = 1;

    /// <summary>
    /// Địa chỉ thanh ghi Dxxxx (word, không có hậu tố bit) cấp giá trị đo thật cho bước này — định nghĩa
    /// qua Import CSV ở tab Set Spec., không sửa trực tiếp trong lưới. Rỗng/null = chưa gán, Giá trị ở tab
    /// Main dùng dữ liệu giả lập (Debug)/0 (Release) như trước.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Địa chỉ thanh ghi Dxxxx (word) cấp trạng thái OK/NG cho bước này — quy ước 1=OK, 2=NG, giá trị khác
    /// coi là chưa xác định. Định nghĩa qua spec-register-map.csv (khóa <c>"{Key}.OkNg"</c>), tương tự
    /// <see cref="Address"/>. Dùng cho cột "OK / NG" bổ sung ở mọi bước, và cho cả cột "Giá trị" của các bước
    /// kiểu kiểm tra tín hiệu (không có giới hạn số, VD "Kiểm tra LED 1") — hiển thị trực tiếp OK/NG thay vì số.
    /// </summary>
    public string? OkNgAddress { get; set; }

    /// <summary>
    /// Địa chỉ thanh ghi Dxxxx (word) PC GHI giá trị LowerLimit/UpperLimit xuống PLC — quy ước diễn giải
    /// giống <see cref="Address"/> (WordSigned, giá trị THÔ, không nhân/chia 100 — PLC/PC tự thống nhất
    /// đơn vị/số chữ số thập phân bên ngoài code này). Định nghĩa qua spec-register-map.csv (khóa
    /// <c>"{Key}.LowerLimit"</c>/<c>"{Key}.UpperLimit"</c>). Chỉ ghi khi Admin/Operator bấm "Xác nhận thay
    /// đổi" ở tab Set spec. (không realtime theo từng ký tự gõ) — xem
    /// SettingTabViewModel.ConfirmChangesAsync/PlcPollingService.WriteStepLimitsAsync.
    /// </summary>
    public string? LowerLimitAddress { get; set; }
    public string? UpperLimitAddress { get; set; }

    public bool IsConfigured => LowerLimit.HasValue || UpperLimit.HasValue;
}
