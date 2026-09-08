namespace EolTester.Core.Models;

public sealed class JobInfo
{
    public string Model { get; set; } = string.Empty;
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string JobName { get; set; } = string.Empty;

    public string JobId => $"{Model}_{Date:yyyyMMdd}_{JobName}";
}
