using EolTester.Core.Models;

namespace EolTester.Configuration.Models;

public sealed class CsvImportResult
{
    public required IReadOnlyList<IoPointDefinition> Points { get; init; }
    public required IReadOnlyList<CsvRowIssue> Warnings { get; init; }
    public required IReadOnlyList<CsvRowIssue> Errors { get; init; }

    public bool HasFatalErrors => Errors.Count > 0;
}
