namespace EolTester.Data;

public interface IAuditLogService
{
    Task LogAsync(string userName, string action, string? oldValue = null, string? newValue = null, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int count, CancellationToken ct = default);
}
