using System.Reflection;

namespace EolTester.Configuration;

/// <summary>
/// 4 file CSV "seed" đi kèm bản build (thư mục <c>SeedData\</c> cạnh exe) được nhúng luôn vào assembly này
/// làm BẢN GỐC để khôi phục khi file trên đĩa bị thiếu. Hai đường dùng:
/// <list type="bullet">
/// <item><b>Khởi động</b> (<c>App.xaml.cs</c>): <see cref="FindMissing"/> → hỏi người dùng (OK khôi phục /
/// Cancel thoát) → <see cref="RestoreMissing"/> ghi lại file thật ra <c>SeedData\</c>.</item>
/// <item><b>Runtime</b> (<see cref="CsvKeyValueFileReader"/>, <see cref="CsvIoMapSeedSource"/>): nếu file đĩa
/// vẫn vắng (VD thư mục chỉ-đọc, ghi lại thất bại) thì đọc thẳng từ resource nhúng — app vẫn chạy đúng.</item>
/// </list>
/// </summary>
public static class SeedFiles
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "spec-register-map.csv",
        "io-map-seed.csv",
        "spec-Default.csv",
        "users-seed.csv",
    };

    private static readonly Assembly Asm = typeof(SeedFiles).Assembly;

    /// <summary>Mở stream nội dung gốc của 1 file seed từ resource nhúng; null nếu tên không phải file seed
    /// hoặc không tìm thấy resource khớp. Khớp theo hậu tố tên file (bỏ qua tiền tố namespace do MSBuild sinh).</summary>
    public static Stream? OpenEmbedded(string fileName)
    {
        var resourceName = Asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        return resourceName is null ? null : Asm.GetManifestResourceStream(resourceName);
    }

    /// <summary>Các file seed đang thiếu trong <paramref name="seedDirectory"/>.</summary>
    public static IReadOnlyList<string> FindMissing(string seedDirectory) =>
        Names.Where(n => !File.Exists(Path.Combine(seedDirectory, n))).ToList();

    /// <summary>Ghi lại các file thiếu từ resource nhúng ra đĩa (giữ nguyên byte gốc, kể cả BOM UTF-8).
    /// Trả về danh sách file KHÔNG ghi được (thư mục chỉ-đọc, thiếu quyền...) — caller quyết định xử lý
    /// (app vẫn chạy nhờ <see cref="OpenEmbedded"/>).</summary>
    public static IReadOnlyList<string> RestoreMissing(string seedDirectory, IEnumerable<string> missing)
    {
        var failed = new List<string>();
        try { Directory.CreateDirectory(seedDirectory); } catch { /* xử lý per-file bên dưới */ }

        foreach (var name in missing)
        {
            try
            {
                using var src = OpenEmbedded(name);
                if (src is null) { failed.Add(name); continue; }
                using var dst = File.Create(Path.Combine(seedDirectory, name));
                src.CopyTo(dst);
            }
            catch
            {
                failed.Add(name);
            }
        }

        return failed;
    }
}
