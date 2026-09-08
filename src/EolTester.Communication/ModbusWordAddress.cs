namespace EolTester.Communication;

/// <summary>
/// Địa chỉ thanh ghi kiểu Mitsubishi D-register: "D1000" = cả từ 16-bit, "D1000.1" = bit thứ 1 trong từ đó.
/// Modbus không có lệnh đọc/ghi 1 bit trong thanh ghi — driver phải đọc/ghi cả từ rồi tự tách/gộp bit.
/// </summary>
public readonly record struct ModbusWordAddress(int WordAddress, int? BitIndex)
{
    public static ModbusWordAddress Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Địa chỉ thanh ghi rỗng.");

        var trimmed = text.Trim();
        if (trimmed.Length < 2 || (trimmed[0] != 'D' && trimmed[0] != 'd'))
            throw new FormatException($"Địa chỉ '{text}' phải bắt đầu bằng 'D' (VD: D1000 hoặc D1000.1).");

        var body = trimmed[1..];
        var dotIndex = body.IndexOf('.');
        if (dotIndex < 0)
        {
            if (!int.TryParse(body, out var word))
                throw new FormatException($"Địa chỉ '{text}' không hợp lệ.");
            return new ModbusWordAddress(word, null);
        }

        var wordPart = body[..dotIndex];
        var bitPart = body[(dotIndex + 1)..];
        if (!int.TryParse(wordPart, out var wordAddr))
            throw new FormatException($"Địa chỉ '{text}' không hợp lệ.");

        // Chỉ số bit chấp nhận cả thập phân ("0".."15") lẫn 1 ký tự hex "A".."F" (10-15) — quy ước phổ
        // biến của các hãng PLC khi đánh số bit trong 1 từ 16-bit (VD D1000.0..D1000.F).
        int bitIdx;
        if (bitPart.Length == 1 && bitPart[0] is (>= 'A' and <= 'F') or (>= 'a' and <= 'f'))
        {
            bitIdx = Convert.ToInt32(bitPart, 16);
        }
        else if (!int.TryParse(bitPart, out bitIdx))
        {
            throw new FormatException($"Địa chỉ '{text}' không hợp lệ.");
        }

        if (bitIdx is < 0 or > 15)
            throw new FormatException($"Địa chỉ '{text}': chỉ số bit phải trong khoảng 0-15.");

        return new ModbusWordAddress(wordAddr, bitIdx);
    }

    /// <summary>Không ném exception — dùng trong vòng lặp polling mỗi tick, nơi địa chỉ kiểu cũ (không phải "Dxxxx") là bình thường, không phải lỗi.</summary>
    public static bool TryParse(string text, out ModbusWordAddress result)
    {
        try
        {
            result = Parse(text);
            return true;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
    }

    public override string ToString() => BitIndex is null ? $"D{WordAddress}" : $"D{WordAddress}.{BitIndex}";
}
