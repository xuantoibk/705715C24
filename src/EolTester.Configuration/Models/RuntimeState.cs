namespace EolTester.Configuration.Models;

/// <summary>
/// Trạng thái runtime cần "nhớ" qua các lần khởi động lại — kể cả khi app bị tắt KHÔNG đúng quy trình
/// (kill process, mất điện). Ghi ra <c>Config\runtime-state.json</c>, debounce ~1s mỗi khi giá trị đổi.
/// Đây là lớp AN TOÀN bổ sung: các thanh ghi cấu hình D100-D199 vốn đã có store riêng (test-parameters.json,
/// spec-profile.json) và tự đẩy lại lúc khởi động; file này giữ thêm <see cref="LastScanOk"/>/
/// <see cref="PreviousSerialNumber"/> (không có store nào khác) và ảnh chụp <see cref="Registers"/> phòng khi
/// có thanh ghi D1xx mới chưa gắn store riêng.
/// </summary>
public sealed class RuntimeState
{
    /// <summary>"Mã Scan ghi nhận" gần nhất — dùng cho check trùng ở SCAN MODE (xem ShellViewModel.CommitScanAsync).</summary>
    public string? LastScanOk { get; set; }

    /// <summary>Số thứ tự serial của lần quét hợp lệ gần nhất trong SCAN MODE (so "phải bằng +1").</summary>
    public int? PreviousSerialNumber { get; set; }

    /// <summary>Ảnh chụp giá trị các thanh ghi cấu hình trong dải D100-D199 (không gồm CMD_ xung), khóa dạng
    /// <c>"D104"</c> → giá trị word.</summary>
    public Dictionary<string, int> Registers { get; set; } = new();
}
