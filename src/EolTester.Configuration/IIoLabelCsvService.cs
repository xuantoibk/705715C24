using EolTester.Configuration.Models;
using EolTester.Core.Models;

namespace EolTester.Configuration;

public interface IIoLabelCsvService
{
    Task ExportAsync(IReadOnlyList<IoPointDefinition> points, string filePath, CancellationToken ct = default);
    Task<CsvImportResult> ImportAsync(string filePath, CancellationToken ct = default);
}
