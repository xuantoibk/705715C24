using System.Text;
using EolTester.Configuration.Models;
using EolTester.Core.Enums;
using EolTester.Core.Models;

namespace EolTester.Configuration;

/// <summary>
/// Đọc/ghi CSV cơ bản (RFC4180, không hỗ trợ xuống dòng trong ô — đủ dùng cho các cột nhãn/địa chỉ ngắn).
/// 1 file CSV dùng chung cho cả Input lẫn Output; hướng I/O suy ra từ tiền tố Key ("X.."=Input,
/// "Y.."=Output) — không có cột Direction riêng, theo đúng yêu cầu đã chốt.
/// CommandAddress (Bit 2 — lệnh)/HandoverAddress (Bit 3 — bàn giao) chỉ có ý nghĩa với Output; để trống ở
/// Input (có cảnh báo nếu Input lỡ điền, không chặn Import).
/// </summary>
public sealed class CsvIoLabelService : IIoLabelCsvService
{
    private const string Header = "Address,Key,Label1,Label2,Label3,CommandAddress,HandoverAddress";

    public async Task ExportAsync(IReadOnlyList<IoPointDefinition> points, string filePath, CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
        await writer.WriteLineAsync(Header);
        foreach (var p in points.OrderBy(p => p.Order))
        {
            ct.ThrowIfCancellationRequested();
            var line = string.Join(",",
                EscapeField(p.Address), EscapeField(p.Key), EscapeField(p.Label1),
                EscapeField(p.Label2 ?? string.Empty), EscapeField(p.Label3 ?? string.Empty),
                EscapeField(p.CommandAddress ?? string.Empty), EscapeField(p.HandoverAddress ?? string.Empty));
            await writer.WriteLineAsync(line);
        }
    }

    public async Task<CsvImportResult> ImportAsync(string filePath, CancellationToken ct = default)
    {
        var points = new List<IoPointDefinition>();
        var warnings = new List<CsvRowIssue>();
        var errors = new List<CsvRowIssue>();
        int order = 0;
        int rowNumber = 0;
        bool skippedHeader = false;

        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!skippedHeader) { skippedHeader = true; continue; }

            var fields = ParseLine(line);
            string address = fields.Count > 0 ? fields[0].Trim() : string.Empty;
            string key = fields.Count > 1 ? fields[1].Trim() : string.Empty;
            string label1 = fields.Count > 2 ? fields[2].Trim() : string.Empty;
            string? label2 = fields.Count > 3 && !string.IsNullOrWhiteSpace(fields[3]) ? fields[3].Trim() : null;
            string? label3 = fields.Count > 4 && !string.IsNullOrWhiteSpace(fields[4]) ? fields[4].Trim() : null;
            string? commandAddress = fields.Count > 5 && !string.IsNullOrWhiteSpace(fields[5]) ? fields[5].Trim() : null;
            string? handoverAddress = fields.Count > 6 && !string.IsNullOrWhiteSpace(fields[6]) ? fields[6].Trim() : null;

            if (string.IsNullOrWhiteSpace(address)) { errors.Add(new CsvRowIssue(rowNumber, CsvRowIssueKind.MissingAddress)); continue; }
            if (string.IsNullOrWhiteSpace(key)) { errors.Add(new CsvRowIssue(rowNumber, CsvRowIssueKind.MissingKey)); continue; }
            if (string.IsNullOrWhiteSpace(label1)) { errors.Add(new CsvRowIssue(rowNumber, CsvRowIssueKind.MissingLabel1)); continue; }

            IoDirection direction;
            if (key.StartsWith("X", StringComparison.OrdinalIgnoreCase)) direction = IoDirection.Input;
            else if (key.StartsWith("Y", StringComparison.OrdinalIgnoreCase)) direction = IoDirection.Output;
            else { errors.Add(new CsvRowIssue(rowNumber, CsvRowIssueKind.UnknownDirection, key)); continue; }

            if (label2 is null) warnings.Add(new CsvRowIssue(rowNumber, CsvRowIssueKind.MissingLabel2));
            if (label3 is null) warnings.Add(new CsvRowIssue(rowNumber, CsvRowIssueKind.MissingLabel3));
            if (direction == IoDirection.Input && (commandAddress is not null || handoverAddress is not null))
            {
                warnings.Add(new CsvRowIssue(rowNumber, CsvRowIssueKind.CommandOrHandoverOnInput, key));
                commandAddress = null;
                handoverAddress = null;
            }

            points.Add(new IoPointDefinition
            {
                Key = key,
                Address = address,
                Direction = direction,
                Label1 = label1,
                Label2 = label2,
                Label3 = label3,
                CommandAddress = commandAddress,
                HandoverAddress = handoverAddress,
                Order = order++,
            });
        }

        return new CsvImportResult { Points = points, Warnings = warnings, Errors = errors };
    }

    private static string EscapeField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
