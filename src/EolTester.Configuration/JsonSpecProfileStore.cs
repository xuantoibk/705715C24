using System.Text.Json;
using EolTester.Configuration.Models;
using EolTester.Core.Enums;
using EolTester.Core.Models;

namespace EolTester.Configuration;

public sealed class JsonSpecProfileStore : ISpecProfileStore
{
    private readonly string _specDirectory;
    private readonly ISpecRegisterMapSource _registerMapSource;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonSpecProfileStore(string configRootDirectory, ISpecRegisterMapSource registerMapSource)
    {
        _specDirectory = Path.Combine(configRootDirectory, "Specs");
        Directory.CreateDirectory(_specDirectory);
        _registerMapSource = registerMapSource;
    }


    private string PathFor(string model)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(model.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return Path.Combine(_specDirectory, $"{safeName}.json");
    }

    public async Task<SpecProfile> LoadAsync(string model, CancellationToken ct = default)
    {
        var path = PathFor(model);
        SpecProfile profile;
        var needsSave = false;
        if (!File.Exists(path))
        {
            profile = CreateSeedProfile(model);
            needsSave = true;
        }
        else
        {
            await using var stream = File.OpenRead(path);
            profile = await JsonSerializer.DeserializeAsync<SpecProfile>(stream, JsonOptions, ct) ?? CreateSeedProfile(model);
        }

        if (EnsureLedSteps(profile)) needsSave = true;

        // "Tốc độ động cơ" (High/Low.voltage) tạm spare — đơn vị "-" và giới hạn 0/0 (PLC không check dưới
        // code). Tự chữa file spec-profile.json cũ (seed trước đây dùng Unit "V", giới hạn 21/25, 10/14) khi
        // nạp, không cần xóa file thủ công.
        foreach (var step in profile.Steps)
        {
            if ((step.Key is "High.voltage" or "Low.voltage") && step.Unit == "V")
            {
                step.Unit = "-";
                step.LowerLimit = 0m;
                step.UpperLimit = 0m;
                needsSave = true;
            }
        }

        if (needsSave) await SaveAsync(profile, ct);


        var addressMap = await _registerMapSource.LoadAsync(ct);
        var scaleMap = await _registerMapSource.LoadScalesAsync(ct);
        foreach (var step in profile.Steps)
        {
            step.Address = addressMap.TryGetValue(step.Key, out var address) ? address : null;
            step.OkNgAddress = addressMap.TryGetValue($"{step.Key}.OkNg", out var okNgAddress) ? okNgAddress : null;
            step.LowerLimitAddress = addressMap.TryGetValue($"{step.Key}.LowerLimit", out var lowerAddress) ? lowerAddress : null;
            step.UpperLimitAddress = addressMap.TryGetValue($"{step.Key}.UpperLimit", out var upperAddress) ? upperAddress : null;
            step.Scale = scaleMap.TryGetValue(step.Key, out var scale) ? scale : 1;
        }

        return profile;
    }

    /// <summary>Các bước kiểu kiểm tra tín hiệu (chỉ đọc OK/NG từ 1 thanh ghi, không có giới hạn số) — không
    /// nằm trong khung cố định High/Low mode, hiển thị ở bảng "Kiểm tra khác" tab Main. OkNgAddress được gán
    /// sau qua spec-register-map.csv (khóa "{Key}.OkNg").</summary>
    private static readonly (string Key, TestMode Mode, string Description)[] AuxiliarySteps =
    [
        ("High.led1", TestMode.High, "Kiểm tra led"),
        // "MMT Quay" giữ Key "Low.led2" cũ để bám đúng thanh ghi lịch sử D66 (Low.led2.OkNg) — khác với
        // "Chiều quay MMT" (High.mmtDir, D69).
        ("Low.led2", TestMode.Low, "MMT Quay"),
        ("High.mmtDir", TestMode.High, "Chiều quay MMT"),
    ];

    private static bool EnsureLedSteps(SpecProfile profile)
    {
        var added = false;
        foreach (var (key, mode, description) in AuxiliarySteps)
        {
            if (profile.Steps.Any(s => s.Key == key)) continue;
            var nextOrder = profile.Steps.Count == 0 ? 0 : profile.Steps.Max(s => s.Order) + 1;
            profile.Steps.Add(new TestStepDefinition { Key = key, Mode = mode, Order = nextOrder, Description = description, Unit = "" });
            added = true;
        }

        return added;
    }

    public async Task SaveAsync(SpecProfile profile, CancellationToken ct = default)
    {
        await using var stream = File.Create(PathFor(profile.Model));
        await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, ct);
    }

    public Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var models = Directory.EnumerateFiles(_specDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Cast<string>()
            .ToList();

        if (models.Count == 0)
        {
            models.Add("705/715");
        }

        return Task.FromResult<IReadOnlyList<string>>(models);
    }

    private static SpecProfile CreateSeedProfile(string model)
    {
        var steps = new List<TestStepDefinition>();
        int order = 0;

        foreach (var mode in new[] { TestMode.High, TestMode.Low })
        {
            steps.Add(new TestStepDefinition { Key = $"{mode}.voltage", Mode = mode, Order = order++, Description = "Tốc độ động cơ", Unit = "-", LowerLimit = 0m, UpperLimit = 0m });
            steps.Add(new TestStepDefinition { Key = $"{mode}.current", Mode = mode, Order = order++, Description = "Dòng điện", Unit = "A", LowerLimit = 500m, UpperLimit = mode == TestMode.High ? 3000m : 1500m });
            steps.Add(new TestStepDefinition { Key = $"{mode}.vacuum", Mode = mode, Order = order++, Description = "Lực hút chân không", Unit = "kPa", LowerLimit = -80m, UpperLimit = -40m });
        }

        var profile = new SpecProfile { Model = model, Steps = steps };
        EnsureLedSteps(profile);
        return profile;
    }
}
