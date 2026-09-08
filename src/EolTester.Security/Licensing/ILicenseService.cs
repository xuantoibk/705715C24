namespace EolTester.Security.Licensing;

public interface ILicenseService
{
    /// <summary>Kiểm tra bản quyền cho máy hiện tại (ổ đĩa C:), tự sinh lại Registration Code nếu chưa có hoặc không khớp máy.</summary>
    LicenseStatus CheckLicense();

    /// <summary>Xác nhận một Registration Key do người dùng nhập; nếu khớp với Registration Code hiện tại thì lưu lại và trả về true.</summary>
    bool TryRegister(string enteredKey);
}
