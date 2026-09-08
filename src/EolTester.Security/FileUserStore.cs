using System.Text.Json;
using System.Text.Json.Serialization;
using EolTester.Core.Enums;
using EolTester.Core.Models;

namespace EolTester.Security;

/// <summary>
/// Nguồn tài khoản đọc từ file thay vì hardcode. Luồng nạp (chạy 1 lần lúc dựng, đồng bộ — danh sách user
/// nhỏ, cần sẵn sàng trước màn hình đăng nhập):
/// <list type="number">
/// <item>Nếu <c>Config\users.json</c> tồn tại → nạp thẳng (đã băm sẵn, không đụng seed).</item>
/// <item>Nếu chưa → đọc <c>SeedData\users-seed.csv</c> (UserName,Role,Password plaintext), băm PBKDF2 từng
/// mật khẩu, ghi ra <c>users.json</c>. Đây là điểm DUY NHẤT plaintext xuất hiện (trong source tree), không
/// bao giờ ghi plaintext ra <c>users.json</c> hay log (CLAUDE.md mục 4).</item>
/// <item>Nếu cả 2 đều thiếu/hỏng → dùng bộ tài khoản mặc định biên dịch sẵn để không khoá cứng app.</item>
/// </list>
/// Đổi tài khoản sau này: sửa <c>users-seed.csv</c> + xoá <c>users.json</c> + khởi động lại.
/// </summary>
public sealed class FileUserStore : IUserStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, UserAccount> _users;

    public FileUserStore(string configDirectory, string seedCsvPath)
    {
        Directory.CreateDirectory(configDirectory);
        var jsonPath = Path.Combine(configDirectory, "users.json");

        _users = TryLoadJson(jsonPath)
                 ?? SeedFromCsvAndPersist(seedCsvPath, jsonPath)
                 ?? BuiltInDefaults();
    }

    public UserAccount? FindByUserName(string userName) =>
        _users.TryGetValue(userName, out var user) ? user : null;

    public IReadOnlyList<string> GetUserNames() => _users.Values.Select(u => u.UserName).ToList();

    private static Dictionary<string, UserAccount>? TryLoadJson(string jsonPath)
    {
        if (!File.Exists(jsonPath)) return null;
        try
        {
            var records = JsonSerializer.Deserialize<List<PersistedUser>>(File.ReadAllText(jsonPath), JsonOptions);
            if (records is null || records.Count == 0) return null;

            var map = new Dictionary<string, UserAccount>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in records)
            {
                if (string.IsNullOrWhiteSpace(r.UserName) || string.IsNullOrEmpty(r.PasswordHash) || string.IsNullOrEmpty(r.PasswordSalt))
                    continue;
                map[r.UserName] = new UserAccount
                {
                    UserName = r.UserName,
                    PasswordHash = r.PasswordHash,
                    PasswordSalt = r.PasswordSalt,
                    Role = r.Role,
                };
            }
            return map.Count > 0 ? map : null;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, UserAccount>? SeedFromCsvAndPersist(string seedCsvPath, string jsonPath)
    {
        if (!File.Exists(seedCsvPath)) return null;
        try
        {
            var map = new Dictionary<string, UserAccount>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(seedCsvPath);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (i == 0 && line.StartsWith("UserName", StringComparison.OrdinalIgnoreCase)) continue; // header

                var parts = line.Split(',');
                if (parts.Length < 3) continue;

                var userName = parts[0].Trim();
                var roleText = parts[1].Trim();
                var password = parts[2].Trim();
                if (userName.Length == 0 || password.Length == 0) continue;
                if (!Enum.TryParse<UserRole>(roleText, ignoreCase: true, out var role)) continue;

                var (hash, salt) = PasswordHasher.HashPassword(password);
                map[userName] = new UserAccount { UserName = userName, PasswordHash = hash, PasswordSalt = salt, Role = role };
            }

            if (map.Count == 0) return null;

            var persisted = map.Values
                .Select(u => new PersistedUser { UserName = u.UserName, Role = u.Role, PasswordHash = u.PasswordHash, PasswordSalt = u.PasswordSalt })
                .ToList();
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(persisted, JsonOptions));
            return map;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, UserAccount> BuiltInDefaults()
    {
        Dictionary<string, UserAccount> Build(params (string Name, string Pwd, UserRole Role)[] defs)
        {
            var map = new Dictionary<string, UserAccount>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, pwd, role) in defs)
            {
                var (hash, salt) = PasswordHasher.HashPassword(pwd);
                map[name] = new UserAccount { UserName = name, PasswordHash = hash, PasswordSalt = salt, Role = role };
            }
            return map;
        }

        return Build(
            ("admin", "admin123", UserRole.Admin),
            ("operator", "operator123", UserRole.Operator),
            ("user", "user123", UserRole.User));
    }

    private sealed class PersistedUser
    {
        public string UserName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
    }
}
