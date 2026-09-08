namespace EolTester.Configuration;

/// <summary>Nguồn thông tin mặc định của dự án (tiêu đề hiển thị theo ngôn ngữ, tên Model mặc định) — đọc từ
/// 1 file CSV cố định trong source tree (SeedData/spec-Default.csv, cùng pattern ISpecRegisterMapSource).
/// Cho phép đổi tên sản phẩm (VD "HV356" -> "705/715") chỉ bằng cách sửa CSV rồi build lại, không phải dò
/// chuỗi hardcode rải rác trong code C#/resx.</summary>
public interface IDefaultProjectInfoSource
{
    Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken ct = default);
}
