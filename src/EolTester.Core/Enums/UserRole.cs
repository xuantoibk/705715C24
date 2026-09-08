namespace EolTester.Core.Enums;

/// <summary>
/// Vai trò người dùng — 3 mức, quyền cao hơn bao gồm mọi quyền của mức thấp hơn (so sánh bằng thứ tự enum).
/// <list type="bullet">
/// <item><see cref="User"/>: vận hành cơ bản tab Main/Monitor (Start/Stop/Reset, Auto/Manual, I/O thủ công,
/// Xác nhận NG/Job). KHÔNG sửa được Set Spec./Setup.</item>
/// <item><see cref="Operator"/>: mọi quyền User + sửa thông số ở tab Set Spec.</item>
/// <item><see cref="Admin"/>: toàn quyền — thêm tab Setup (kết nối PLC) + khối XUẤT FILE CSV KẾT QUẢ +
/// Import/Export nhãn I/O.</item>
/// </list>
/// Khách (chưa đăng nhập): chỉ chuyển tab + quét barcode để vận hành máy (SCAN MODE), không thao tác gì khác.
/// </summary>
public enum UserRole
{
    User,
    Operator,
    Admin
}
