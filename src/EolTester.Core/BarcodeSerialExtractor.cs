namespace EolTester.Core;

/// <summary>
/// SCAN MODE (xem ShellViewModel.CommitScanAsync): trích xuất số thứ tự serial từ 1 đoạn con của chuỗi
/// barcode đã quét, theo vị trí ký tự bắt đầu/kết thúc cấu hình ở Set Spec (TestParameters.SerialStartIndex/
/// SerialEndIndex, 1-based — ký tự đầu tiên của chuỗi là vị trí 1, khớp cách người dùng mô tả trên UI).
/// </summary>
public static class BarcodeSerialExtractor
{
    public static bool TryExtractSerialNumber(string? barcode, int startIndex, int endIndex, out int serialNumber)
    {
        serialNumber = 0;
        if (string.IsNullOrEmpty(barcode) || startIndex < 1 || endIndex < startIndex) return false;

        var zeroBasedStart = startIndex - 1;
        if (zeroBasedStart >= barcode.Length) return false;

        var length = Math.Min(endIndex, barcode.Length) - zeroBasedStart;
        if (length <= 0) return false;

        var segment = barcode.Substring(zeroBasedStart, length);
        return int.TryParse(segment, out serialNumber);
    }
}
