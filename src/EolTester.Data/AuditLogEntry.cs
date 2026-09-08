namespace EolTester.Data;

public sealed class AuditLogEntry
{
    public int Id { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string UserName { get; init; }
    public required string Action { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}
