using System.Text.RegularExpressions;

namespace EolTester.Core;

/// <summary>
/// Nhận diện 1 phần tử trong "Cột dữ liệu tùy chỉnh" (CSV kết quả, tách theo dấu phẩy ở tầng gọi) thành
/// <see cref="CsvColumnSpec"/> — token đặc biệt (&lt;STT&gt;/&lt;barcode&gt;/&lt;JOB&gt;/&lt;date&gt;/
/// &lt;time&gt;/composite ngày-giờ) hoặc thanh ghi PLC (mặc định nếu không khớp token nào).
/// </summary>
public static class CsvColumnSpecParser
{
    private static readonly (string Token, string DotNetFormat, bool IsTimePart)[] DateTimeSubTokens =
    [
        ("<yyyy>", "yyyy", false),
        ("<mm>", "MM", false),
        ("<dd>", "dd", false),
        ("<hh>", "HH", true),
        ("<min>", "mm", true),
        ("<sec>", "ss", true),
    ];

    public static CsvColumnSpec Parse(string rawToken)
    {
        var trimmed = rawToken.Trim();

        if (Equals(trimmed, "<STT>")) return new CsvColumnSpec.Sequence();
        if (Equals(trimmed, "<barcode>")) return new CsvColumnSpec.BarcodeColumn();
        if (Equals(trimmed, "<JOB>")) return new CsvColumnSpec.JobIdColumn();
        if (Equals(trimmed, "<date>")) return new CsvColumnSpec.DateTimeColumn(IsTime: false, CustomFormat: null);
        if (Equals(trimmed, "<time>")) return new CsvColumnSpec.DateTimeColumn(IsTime: true, CustomFormat: null);

        var (formatted, hasDatePart, hasTimePart) = SubstituteDateTimeSubTokens(trimmed);
        if (hasDatePart || hasTimePart)
        {
            // Cả 2 loại sub-token cùng xuất hiện trong 1 cột (hiếm gặp) — ưu tiên "Date" làm header, giữ
            // nguyên format đã ghép cả ngày lẫn giờ trong giá trị.
            return new CsvColumnSpec.DateTimeColumn(IsTime: hasTimePart && !hasDatePart, CustomFormat: formatted);
        }

        return new CsvColumnSpec.RegisterColumn(trimmed);
    }

    private static bool Equals(string value, string token) =>
        string.Equals(value, token, StringComparison.OrdinalIgnoreCase);

    private static (string Result, bool HasDate, bool HasTime) SubstituteDateTimeSubTokens(string text)
    {
        var result = text;
        var hasDate = false;
        var hasTime = false;

        foreach (var (token, format, isTimePart) in DateTimeSubTokens)
        {
            if (!result.Contains(token, StringComparison.OrdinalIgnoreCase)) continue;

            result = Regex.Replace(result, Regex.Escape(token), format, RegexOptions.IgnoreCase);
            if (isTimePart) hasTime = true; else hasDate = true;
        }

        return (result, hasDate, hasTime);
    }
}
