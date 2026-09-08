using System.Linq;

namespace EolTester.Communication;

/// <summary>
/// Bảng thanh ghi trung gian trong bộ nhớ PC — "biến D1005" truy cập bằng đúng tên PLC dùng
/// (VD "D1005" cho cả từ, "D1005.1" cho 1 bit trong từ đó). Chỉ đọc/ghi được trong đúng
/// phạm vi đã cấu hình ở tab Setup (InputBlock/OutputBlock) — ràng buộc nghiệp vụ, không
/// phải giới hạn kỹ thuật của riêng 1 giao thức, để tránh đọc/ghi nhầm sang vùng D không thuộc quyền quản lý
/// của app. Thread-safe (bọc <c>Lock</c>) — khi PC làm Slave, luồng lắng nghe (Modbus/MC Protocol/SLMP) và
/// luồng UI/nghiệp vụ cùng đọc/ghi bảng này đồng thời.
/// <para>
/// Đây là "ngôn ngữ chung nội bộ" dùng xuyên suốt mọi giao thức (Modbus RTU, MC Protocol, SLMP — cùng họ địa
/// chỉ D-register/word) — tên lớp cố ý trung lập (không gắn riêng "Modbus") để phản ánh đúng vai trò kiến
/// trúc: business logic (ViewModel các tab) chỉ đọc/ghi bảng này, không bao giờ gọi thẳng driver — xem
/// CLAUDE.md mục "Kiến trúc lõi: Bảng thanh ghi trung gian dùng chung".
/// </para>
/// </summary>
public sealed class PlcRegisterImage
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, ushort> _words = new();
    private readonly List<(int Start, int End)> _allowedRanges = [];

    public bool CanReadAddress(int wordAddress)
    {
        if (wordAddress is >= 0 and < 100) return true;
        if (!IsInConfiguredRange(wordAddress)) return false;
        return wordAddress >= 200;
    }

    public bool CanWriteAddress(int wordAddress)
    {
        if (wordAddress is >= 100 and < 200) return true;
        if (!IsInConfiguredRange(wordAddress)) return false;
        return wordAddress >= 200;
    }

    /// <summary>
    /// BUG THẬT đã gặp: trước đây xóa sạch <see cref="_words"/> vô điều kiện mỗi lần gọi — hàm này được gọi cả
    /// lúc khởi động app lẫn mỗi lần Admin bấm "Lưu cấu hình" (kể cả khi chỉ đổi ComPort/Parity để RECONNECT
    /// sau khi mất kết nối, không hề đổi Input/Output Block). Cache bị xóa sạch khiến tick polling kế tiếp
    /// (<c>PlcPollingService.RefreshRuntimeStateAsync</c> Phase 2 — vòng ghi tuần hoàn D100-D199) đọc
    /// <c>TryGetWord</c> thất bại, lấy giá trị mặc định 0, rồi GHI 0 THẬT xuống PLC — xóa mất giá trị PLC đang
    /// giữ ở D100-D199 (VD CMD_MODE) chỉ vì Admin bấm Lưu để khắc phục sự cố kết nối. Đã sửa: chỉ loại bỏ đúng
    /// những entry thực sự nằm ngoài phạm vi hợp lệ MỚI (cả 2 dải cố định D0-D199 lẫn dải cấu hình D200-D2000),
    /// giữ nguyên mọi giá trị vẫn còn hợp lệ.
    /// </summary>
    public void ConfigureAllowedRanges(IEnumerable<(int Start, int Count)> ranges)
    {
        lock (_gate)
        {
            _allowedRanges.Clear();
            foreach (var (start, count) in ranges)
                _allowedRanges.Add((start, start + count - 1));

            var staleKeys = _words.Keys.Where(addr => !CanReadAddress(addr) && !CanWriteAddress(addr)).ToList();
            foreach (var key in staleKeys) _words.Remove(key);
        }
    }

    private bool IsInConfiguredRange(int wordAddress) => _allowedRanges.Any(r => wordAddress >= r.Start && wordAddress <= r.End);

    /// <summary>Lưu nhiều giá trị đọc được liên tiếp vào cache cùng lúc — dùng cho vòng polling đọc-theo-khối
    /// (1 request Modbus FC03/nhiều thanh ghi thay vì N request 1-thanh-ghi, xem PlcPollingService). Bỏ qua
    /// (không throw) từng địa chỉ không được phép đọc, để 1 khối lỡ chồng lấn ranh giới D100-D199 (chỉ-ghi)
    /// hoặc nằm ngoài range đã cấu hình vẫn lưu được phần còn lại hợp lệ — cùng tinh thần "best-effort" với
    /// <see cref="TryStoreReadValue"/> (dùng khi đọc từng thanh ghi lẻ).</summary>
    public void StoreReadBlock(int startAddress, IReadOnlyList<ushort> words)
    {
        lock (_gate)
        {
            for (var i = 0; i < words.Count; i++)
            {
                var addr = startAddress + i;
                if (CanReadAddress(addr)) _words[addr] = words[i];
            }
        }
    }

    /// <summary>Lấy nhiều giá trị liên tiếp từ cache để ghi khối xuống PLC (1 request Modbus FC16/nhiều thanh
    /// ghi thay vì N request). Địa chỉ chưa từng có giá trị trong cache trả về 0 — cùng quy ước mặc định với
    /// <see cref="TryGetWord"/>.</summary>
    public ushort[] SnapshotBlock(int startAddress, int count)
    {
        lock (_gate)
        {
            var result = new ushort[count];
            for (var i = 0; i < count; i++)
            {
                _words.TryGetValue(startAddress + i, out var w);
                result[i] = w;
            }
            return result;
        }
    }

    /// <summary>Cập nhật 1 từ đơn lẻ vào cache — chấp nhận các địa chỉ được phép ghi theo quy tắc master mode.</summary>
    public bool TryUpdateWord(int wordAddress, ushort value)
    {
        lock (_gate)
        {
            if (!CanWriteAddress(wordAddress)) return false;
            _words[wordAddress] = value;
            return true;
        }
    }

    /// <summary>Ghi đè trực tiếp vào cache, bỏ qua CanWriteAddress — chỉ dùng cho "Giám sát DATA" ở tab Setup
    /// khi Admin chủ động gõ giá trị thủ công (kể cả D0-D99 vốn chỉ-đọc theo quy tắc cứng, kể cả khi mất kết
    /// nối PLC). Đây là hành vi có chủ đích: Admin cần xem/thử giá trị ngay lập tức mà không phụ thuộc PLC
    /// thật đang online hay không; nếu đang polling thật, lần đọc thật tiếp theo (~500ms) sẽ ghi đè lại.</summary>
    public void ForceSetWordForAdminOverride(int wordAddress, ushort value)
    {
        lock (_gate)
        {
            _words[wordAddress] = value;
        }
    }

    public bool TrySetWordForMasterWrite(int wordAddress, ushort value)
    {
        lock (_gate)
        {
            if (!CanWriteAddress(wordAddress)) return false;
            _words[wordAddress] = value;
            return true;
        }
    }

    public bool TryStoreReadValue(int wordAddress, ushort value)
    {
        lock (_gate)
        {
            if (!CanReadAddress(wordAddress)) return false;
            _words[wordAddress] = value;
            return true;
        }
    }

    public bool TryGetWord(int wordAddress, out ushort value)
    {
        lock (_gate)
        {
            if (!IsInConfiguredRange(wordAddress) && wordAddress is < 0 or >= 200) { value = 0; return false; }
            return _words.TryGetValue(wordAddress, out value);
        }
    }

    public bool TryGetBit(int wordAddress, int bitIndex, out bool value)
    {
        if (!TryGetWord(wordAddress, out var word)) { value = false; return false; }
        value = (word & (1 << bitIndex)) != 0;
        return true;
    }

    public bool SetBit(int wordAddress, int bitIndex, bool value)
    {
        lock (_gate)
        {
            if (!IsInConfiguredRange(wordAddress)) return false;
            var word = _words.TryGetValue(wordAddress, out var existing) ? existing : (ushort)0;
            _words[wordAddress] = value ? (ushort)(word | (1 << bitIndex)) : (ushort)(word & ~(1 << bitIndex));
            return true;
        }
    }

    /// <summary>Truy cập bằng đúng cú pháp PLC: "D1005" (cả từ) hoặc "D1005.1" (1 bit).</summary>
    public bool TryGetValue(string address, out int value)
    {
        var addr = ModbusWordAddress.Parse(address);
        if (addr.BitIndex is int bit)
        {
            var ok = TryGetBit(addr.WordAddress, bit, out var b);
            value = b ? 1 : 0;
            return ok;
        }
        var okWord = TryGetWord(addr.WordAddress, out var w);
        value = (short)w; // Diễn giải word là signed — khớp quy ước dùng ở PlcPollingService.ReadMeasurement/ReadRegisterLimit.
        return okWord;
    }

    /// <summary>Toàn bộ giá trị hiện có trong đúng phạm vi đã cấu hình — dùng để nạp vào DataStore của Slave lúc khởi động/đồng bộ định kỳ.</summary>
    public IReadOnlyDictionary<int, ushort> SnapshotAll()
    {
        lock (_gate)
        {
            return new Dictionary<int, ushort>(_words);
        }
    }
}
