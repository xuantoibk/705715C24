using Microsoft.Data.Sqlite;

namespace EolTester.Data;

public sealed class SqliteAuditLogService : IAuditLogService
{
    private readonly string _connectionString;

    public SqliteAuditLogService(string databaseFilePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databaseFilePath)!);
        _connectionString = $"Data Source={databaseFilePath}";
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                UserName TEXT NOT NULL,
                Action TEXT NOT NULL,
                OldValue TEXT NULL,
                NewValue TEXT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public async Task LogAsync(string userName, string action, string? oldValue = null, string? newValue = null, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AuditLog (Timestamp, UserName, Action, OldValue, NewValue)
            VALUES ($timestamp, $userName, $action, $oldValue, $newValue);
            """;
        command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$userName", userName);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$oldValue", (object?)oldValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$newValue", (object?)newValue ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Timestamp, UserName, Action, OldValue, NewValue
            FROM AuditLog
            ORDER BY Id DESC
            LIMIT $count;
            """;
        command.Parameters.AddWithValue("$count", count);

        var results = new List<AuditLogEntry>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new AuditLogEntry
            {
                Id = reader.GetInt32(0),
                Timestamp = DateTime.Parse(reader.GetString(1)),
                UserName = reader.GetString(2),
                Action = reader.GetString(3),
                OldValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                NewValue = reader.IsDBNull(5) ? null : reader.GetString(5),
            });
        }

        return results;
    }
}
