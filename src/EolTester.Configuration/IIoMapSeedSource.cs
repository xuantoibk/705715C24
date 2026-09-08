using EolTester.Core.Models;

namespace EolTester.Configuration;

/// <summary>Nguồn dữ liệu khởi tạo (seed) cho bản đồ I/O mặc định — chỉ dùng đúng 1 lần lúc chưa có io-map.json.</summary>
public interface IIoMapSeedSource
{
    Task<IReadOnlyList<IoPointDefinition>?> LoadAsync(CancellationToken ct = default);
}
