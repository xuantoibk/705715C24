namespace EolTester.Configuration.Models;

public enum CsvRowIssueKind
{
    MissingAddress,
    MissingKey,
    MissingLabel1,
    UnknownDirection,
    MissingLabel2,
    MissingLabel3,
    CommandOrHandoverOnInput,
}


public sealed record CsvRowIssue(int RowNumber, CsvRowIssueKind Kind, string? Detail = null);
