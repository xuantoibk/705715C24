using System.Runtime.InteropServices;
using System.Text;

namespace KeygenApp;

/// <summary>
/// C# port of 00_KeygenV2.LSP (BXT keygen). Every function below is a direct,
/// line-by-line transliteration of the corresponding AutoLISP `defun`, using
/// 1-based <see cref="Substr"/>/<see cref="Ascii"/> helpers that mirror AutoLISP's
/// substr/ascii semantics exactly. Do not "simplify" the index arithmetic without
/// re-checking against the .LSP source — the permutation only round-trips correctly
/// if every substr offset matches the original 1-based positions.
/// </summary>
public static class BxtKeygen
{
    /// <summary>HM_idx from the LSP: (1 3 4 2 5 7 8 6)</summary>
    public static readonly int[] HmIdx = { 1, 3, 4, 2, 5, 7, 8, 6 };

    public const string RegistryKeyPath = @"SOFTWARE\Autodesk\autolisp\BXT";

    // ---- AutoLISP-style string primitives (1-based, like substr/ascii/chr) ----

    private static string Substr(string s, int start, int len)
    {
        if (len <= 0) return "";
        if (start < 1) start = 1;
        int zeroStart = start - 1;
        if (zeroStart >= s.Length) return "";
        int actualLen = Math.Min(len, s.Length - zeroStart);
        return actualLen <= 0 ? "" : s.Substring(zeroStart, actualLen);
    }

    private static int Ascii(string s) => s.Length == 0 ? 0 : s[0];

    private static string Reverse(string s)
    {
        var arr = s.ToCharArray();
        Array.Reverse(arr);
        return new string(arr);
    }

    // ---- STD-NUM->HEX / STD-HEX->NUM ----

    public static string StdNumToHex(long i)
    {
        if (i == 0) return "0";
        var sb = new StringBuilder();
        while (i > 0)
        {
            long a = i % 16;
            i >>= 4;
            char c = a < 10 ? (char)('0' + a) : (char)('A' + (a - 10));
            sb.Insert(0, c);
        }
        return sb.ToString();
    }

    public static long StdHexToNum(string s)
    {
        s = s.ToUpperInvariant();
        if (s.Length >= 2 && s.Substring(0, 2) == "0X") s = s.Substring(2);
        long n = 0;
        for (int i = 1; i <= s.Length; i++)
        {
            string a = Substr(s, i, 1);
            int digit = a[0] <= '9' ? a[0] - '0' : a[0] - 55; // '0'=48, 'A'-55=10
            n = (n << 4) + digit;
        }
        return n;
    }

    // ---- HDSerial: volume serial number of a drive, same source data as
    //      Scripting.FileSystemObject's Drive.SerialNumber (both call
    //      GetVolumeInformation under the hood) ----

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder? fileSystemNameBuffer,
        int fileSystemNameSize);

    public static uint GetRawDriveSerial(string driveLetter)
    {
        if (!GetVolumeInformation(driveLetter + ":\\", null, 0, out uint serial, out _, out _, null, 0))
            throw new InvalidOperationException($"Could not read volume serial for drive {driveLetter}:");
        return serial;
    }

    /// <summary>Equivalent of (STD-NUM->HEX (abs (HDSerial drive))), zero-padded to 8 hex chars.</summary>
    public static string GetHddSerialHex(string driveLetter)
    {
        uint raw = GetRawDriveSerial(driveLetter);
        int signed = unchecked((int)raw); // FSO's SerialNumber is a signed 32-bit Long over the same DWORD
        long absVal = Math.Abs((long)signed);
        string hex = StdNumToHex(absVal);
        while (hex.Length < 8) hex = "0" + hex;
        return hex;
    }

    // ---- bxt_MAP_serial (encode) ----

    public static string MapSerial(int[] hmIdx, string hddC, string hexTime)
    {
        string hexTimeRev = Reverse(hexTime);

        var hexFb = new StringBuilder();
        foreach (int index in hmIdx)
        {
            hexFb.Append(Substr(hddC, index, 1));
            hexFb.Append(Substr(hexTimeRev, index, 1));
        }
        string hexFbStr = hexFb.ToString();

        var serialFb = new StringBuilder();
        foreach (int index in hmIdx)
        {
            long v1 = StdHexToNum(Substr(hexFbStr, index, 1));
            long v2 = StdHexToNum(Substr(hexFbStr, index + 8, 1));
            serialFb.Append((char)(65 + index + v1));
            serialFb.Append((char)(65 + index + v2));
        }
        string sf = serialFb.ToString();

        return $"{Substr(sf, 1, 4)}-{Substr(sf, 5, 4)}-{Substr(sf, 9, 4)}-{Substr(sf, 13, 4)}";
    }

    // ---- bxt_get_nthlist ----

    public static List<int> GetNthList(int[] hmIdx)
    {
        var result = new List<int>();
        foreach (int lcValue in new[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            for (int lcIdx = 0; lcIdx < hmIdx.Length; lcIdx++)
            {
                if (hmIdx[lcIdx] == lcValue)
                {
                    result.Add(lcIdx + 1);
                    break;
                }
            }
        }
        return result;
    }

    // ---- bxt_unbxt_MAP_serial (decode) ----
    // Returns 16 hex chars: 8 for the (reversed) HDD serial, 8 for the (reversed) time,
    // exactly like the LSP function — callers must Reverse() each half themselves.

    public static string UnmapSerial(string serialFb, int[] hmIdx)
    {
        string hexFb = Substr(serialFb, 1, 4) + Substr(serialFb, 6, 4) + Substr(serialFb, 11, 4) + Substr(serialFb, 16, 4);

        string str1 = "", str2 = "";
        for (int index = 0; index < 8; index++)
        {
            long lc = Ascii(Substr(hexFb, 2 * index + 1, 1)) - (65 + hmIdx[index]);
            str1 += (lc >= 0 && lc <= 15) ? StdNumToHex(lc) : "0";

            lc = Ascii(Substr(hexFb, 2 * index + 2, 1)) - (65 + hmIdx[index]);
            str2 += (lc >= 0 && lc <= 15) ? StdNumToHex(lc) : "0";
        }
        hexFb = str1 + str2;

        var idxList = GetNthList(hmIdx);
        str1 = ""; str2 = "";
        for (int i = 0; i < 8; i++)
        {
            str1 += Substr(hexFb, idxList[i], 1);
            str2 += Substr(hexFb, idxList[i] + 8, 1);
        }
        hexFb = str1 + str2;

        str1 = ""; str2 = "";
        for (int i = 0; i < 8; i++)
        {
            str1 += Substr(hexFb, 2 * idxList[i] - 1, 1);
            str2 += Substr(hexFb, 2 * idxList[i], 1);
        }
        str2 = Reverse(str2);

        return str1 + str2;
    }

    /// <summary>True if key_error would have been set: any decoded nibble fell outside 0..15.</summary>
    public static bool HasDecodeError(string serialFb, int[] hmIdx)
    {
        string hexFb = Substr(serialFb, 1, 4) + Substr(serialFb, 6, 4) + Substr(serialFb, 11, 4) + Substr(serialFb, 16, 4);
        for (int index = 0; index < 8; index++)
        {
            long lc = Ascii(Substr(hexFb, 2 * index + 1, 1)) - (65 + hmIdx[index]);
            if (lc < 0 || lc > 15) return true;
            lc = Ascii(Substr(hexFb, 2 * index + 2, 1)) - (65 + hmIdx[index]);
            if (lc < 0 || lc > 15) return true;
        }
        return false;
    }

    // ---- BXT_serial_create ----
    // (rtos (getvar "CDATE") 2 0) rounds CDATE (YYYYMMDD.HHMMSSmmm) to 0 decimals.
    // Since the fractional part is always < 0.24 (HH never reaches 50), it always
    // rounds down to today's date, so this is equivalent to just using yyyyMMdd.

    public static string CreateRegistrationCode(string hddCRaw, int[] hmIdx, DateTime? now = null)
    {
        string hddC = hddCRaw;
        while (hddC.Length < 8) hddC = "0" + hddC;

        string dateStr = (now ?? DateTime.Now).ToString("yyyyMMdd");
        string composed = "2" + Substr(dateStr, 3, 3) + "1" + Substr(dateStr, 6, 3);
        long timeNum = long.Parse(composed);
        string hexTime = StdNumToHex(timeNum);
        while (hexTime.Length < 8) hexTime = "0" + hexTime;

        return MapSerial(hmIdx, hddC, hexTime);
    }

    // ---- format validation, mirrors the checks in bxt_keygen's vlx_getgen ----

    public static bool ValidateCodeFormat(string code)
    {
        if (code.Length != 19) return false;
        if (code[4] != '-' || code[9] != '-' || code[14] != '-') return false;
        for (int i = 0; i < 19; i++)
        {
            if (i == 4 || i == 9 || i == 14) continue;
            char c = code[i];
            if (c < 'B' || c > 'X') return false;
        }
        return true;
    }

    /// <summary>
    /// Given a Registration Code, computes the matching Registration Key
    /// (unmap then re-map, like vlx_getgen / vlx_genreg) and returns the
    /// embedded HDD hex so callers can check it against the local machine.
    /// </summary>
    public static (string key, string embeddedHddHex) ComputeKeyFromCode(string code, int[] hmIdx)
    {
        string unmapped = UnmapSerial(code, hmIdx);
        string hddRev = Substr(unmapped, 1, 8);
        string dateRev = Substr(unmapped, 9, 8);
        string hddNormal = Reverse(hddRev);
        string dateNormal = Reverse(dateRev);
        string key = MapSerial(hmIdx, hddNormal, dateNormal);
        return (key, hddRev);
    }
}
