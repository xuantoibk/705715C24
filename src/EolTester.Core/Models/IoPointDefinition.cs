using EolTester.Core.Enums;

namespace EolTester.Core.Models;

public sealed class IoPointDefinition
{
    public required string Key { get; init; }
    public required string Address { get; init; }
    public required IoDirection Direction { get; init; }
    public required string Label1 { get; set; }
    public string? Label2 { get; set; }
    public string? Label3 { get; set; }
    public required int Order { get; init; }

    /// <summary>
    /// Bit 2 (lệnh) — chỉ có ý nghĩa với Output: PC ghi khi Admin bấm ON/OFF ở chế độ Thủ công, tách khỏi
    /// <see cref="Address"/> (Bit 1 — trạng thái, PLC báo về) để tránh PC ghi đè lên bit PLC đang tự quản lý.
    /// Rỗng = điểm này chưa nâng cấp lên 3-bit, tiếp tục ghi thẳng vào Address như trước (tương thích ngược).
    /// </summary>
    public string? CommandAddress { get; set; }

    /// <summary>
    /// Bit 3 (bàn giao) — trạng thái PLC đang tự quản lý lần cuối trước khi chuyển Auto→Manual. Đọc đúng 1
    /// lần lúc chuyển đổi rồi copy sang <see cref="CommandAddress"/> ("bumpless transfer" — tránh ngõ ra
    /// nhảy trạng thái đột ngột khi giao quyền điều khiển cho người vận hành). Chỉ có ý nghĩa với Output.
    /// </summary>
    public string? HandoverAddress { get; set; }

    /// <summary>Nhãn hiển thị theo chỉ số ngôn ngữ hiện tại (0/1/2) — rơi về Label1 nếu slot tương ứng trống.</summary>
    public string GetLabel(int languageIndex) => languageIndex switch
    {
        1 => string.IsNullOrWhiteSpace(Label2) ? Label1 : Label2,
        2 => string.IsNullOrWhiteSpace(Label3) ? Label1 : Label3,
        _ => Label1,
    };
}
