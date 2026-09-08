namespace EolTester.Core.Models;

public sealed class TestResult
{
    public required string JobId { get; init; }
    public required string Serial { get; init; }
    public required DateTime Timestamp { get; init; }
    public required IReadOnlyList<TestStepResult> Steps { get; init; }

    public bool OverallPass => Steps.All(s => s.Passed != false);
}
