using EolTester.Configuration;
using EolTester.Configuration.Models;
using EolTester.Core.Enums;
using EolTester.Core.Models;

namespace EolTester.App;

/// <summary>
/// Bộ bước đo mặc định + tự "chữa lành" ĐẶC THÙ máy 705715-C24 — khung High/Low mode × Tốc độ động cơ/Dòng
/// điện/Lực hút chân không (Tốc độ động cơ tạm spare, đơn vị "-"), cộng bảng "Kiểm tra khác" gồm 3 bước kiểu
/// OK/NG thuần (Kiểm tra led, MMT Quay, Chiều quay MMT/mmtDir) — implement <see cref="ISpecProfileSeeder"/>
/// (dùng chung, EolTester.Platform.Wpf). Xem MainTabViewModel.OtherCheckSteps.
/// </summary>
public sealed class Eol705715C24SpecProfileSeeder : ISpecProfileSeeder
{
    /// <summary>Các bước kiểu kiểm tra tín hiệu (chỉ đọc OK/NG từ 1 thanh ghi, không có giới hạn số) — không
    /// nằm trong khung cố định High/Low mode, hiển thị ở bảng "Kiểm tra khác" tab Main. OkNgAddress được gán
    /// sau qua spec-register-map.csv (khóa "{Key}.OkNg").</summary>
    private static readonly (string Key, TestMode Mode, string Description, string ResxKey)[] AuxiliarySteps =
    [
        ("High.led1", TestMode.High, "Kiểm tra led", "Spec_Led1Check"),
        // "MMT Quay" giữ Key "Low.led2" cũ để bám đúng thanh ghi lịch sử D66 (Low.led2.OkNg) — khác với
        // "Chiều quay MMT" (High.mmtDir, D69). Khóa resx riêng của máy này (Spec_MmtRunCheck/Spec_MmtDirCheck)
        // — KHÔNG dùng "Spec_Led2Check" (đó là khóa của máy 705/715, ý nghĩa khác hẳn).
        ("Low.led2", TestMode.Low, "MMT Quay", "Spec_MmtRunCheck"),
        ("High.mmtDir", TestMode.High, "Chiều quay MMT", "Spec_MmtDirCheck"),
    ];

    public SpecProfile CreateSeedProfile(string model)
    {
        var steps = new List<TestStepDefinition>();
        var order = 0;

        foreach (var mode in new[] { TestMode.High, TestMode.Low })
        {
            steps.Add(new TestStepDefinition { Key = $"{mode}.voltage", Mode = mode, Order = order++, Description = "Tốc độ động cơ", DescriptionResxKey = "Spec_Voltage", Unit = "-", LowerLimit = 0m, UpperLimit = 0m });
            steps.Add(new TestStepDefinition { Key = $"{mode}.current", Mode = mode, Order = order++, Description = "Dòng điện", DescriptionResxKey = "Spec_Current", Unit = "A", LowerLimit = 500m, UpperLimit = mode == TestMode.High ? 3000m : 1500m });
            steps.Add(new TestStepDefinition { Key = $"{mode}.vacuum", Mode = mode, Order = order++, Description = "Lực hút chân không", DescriptionResxKey = "Spec_Vacuum", Unit = "kPa", LowerLimit = -80m, UpperLimit = -40m });
        }

        var profile = new SpecProfile { Model = model, Steps = steps };
        ApplySelfHealing(profile);
        return profile;
    }

    private static readonly Dictionary<string, string> FixedStepResxKeys = new()
    {
        ["High.voltage"] = "Spec_Voltage",
        ["Low.voltage"] = "Spec_Voltage",
        ["High.current"] = "Spec_Current",
        ["Low.current"] = "Spec_Current",
        ["High.vacuum"] = "Spec_Vacuum",
        ["Low.vacuum"] = "Spec_Vacuum",
        ["High.led1"] = "Spec_Led1Check",
        ["Low.led2"] = "Spec_MmtRunCheck",
        ["High.mmtDir"] = "Spec_MmtDirCheck",
    };

    public bool ApplySelfHealing(SpecProfile profile)
    {
        var added = false;
        foreach (var (key, mode, description, resxKey) in AuxiliarySteps)
        {
            if (profile.Steps.Any(s => s.Key == key)) continue;
            var nextOrder = profile.Steps.Count == 0 ? 0 : profile.Steps.Max(s => s.Order) + 1;
            profile.Steps.Add(new TestStepDefinition { Key = key, Mode = mode, Order = nextOrder, Description = description, DescriptionResxKey = resxKey, Unit = "" });
            added = true;
        }

        // "Tốc độ động cơ" (High/Low.voltage) tạm spare — đơn vị "-" và giới hạn 0/0. Tự chữa file
        // spec-profile.json cũ (seed trước đây dùng Unit "V", giới hạn 21/25, 10/14) khi nạp.
        foreach (var step in profile.Steps)
        {
            if (step.Key is "High.voltage" or "Low.voltage" && step.Unit == "V")
            {
                step.Unit = "-";
                step.LowerLimit = 0m;
                step.UpperLimit = 0m;
                added = true;
            }
        }

        // Tự "vá" DescriptionResxKey cho file spec-profile.json cũ lưu từ trước khi có field này (tách khỏi
        // switch hardcode trong TestStepRowViewModel — xem docs/session-log/history.md).
        foreach (var step in profile.Steps)
        {
            if (step.DescriptionResxKey is null && FixedStepResxKeys.TryGetValue(step.Key, out var resxKey))
            {
                step.DescriptionResxKey = resxKey;
                added = true;
            }
        }

        return added;
    }
}
