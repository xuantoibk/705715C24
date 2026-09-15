using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EolTester.App.Localization;
using EolTester.Core;
using EolTester.Core.Models;

namespace EolTester.App.ViewModels.Rows;

public partial class TestStepRowViewModel : ObservableObject
{
    public TestStepRowViewModel(TestStepDefinition definition)
    {
        Definition = definition;
        _lowerLimit = definition.LowerLimit;
        _upperLimit = definition.UpperLimit;
        _editLowerLimit = definition.LowerLimit;
        _editUpperLimit = definition.UpperLimit;
    }

    public TestStepDefinition Definition { get; }

    /// <summary>Tên bước hiển thị — với các bước thuộc khung cố định (voltage/current/vacuum + kiểm tra tín
    /// hiệu led1/led2/mmtDir, giống nhau ở mọi model, chỉ khác giới hạn) tra qua resx (Spec_Voltage/Spec_Current/
    /// Spec_Vacuum/Spec_Led1Check/Spec_MmtRunCheck/Spec_MmtDirCheck) để tự đổi theo cờ VI/EN; bước lạ (ngoài các
    /// khóa này, nếu model sau này thêm) rơi về đúng chuỗi đã lưu trong spec-profile.json
    /// (Definition.Description) — không có resx tương ứng thì không có gì để tự đổi ngôn ngữ.</summary>
    public string Description => Definition.Key switch
    {
        "High.voltage" or "Low.voltage" => Translation.Instance["Spec_Voltage"],
        "High.current" or "Low.current" => Translation.Instance["Spec_Current"],
        "High.vacuum" or "Low.vacuum" => Translation.Instance["Spec_Vacuum"],
        "High.led1" => Translation.Instance["Spec_Led1Check"],
        "Low.led2" => Translation.Instance["Spec_MmtRunCheck"],
        "High.mmtDir" => Translation.Instance["Spec_MmtDirCheck"],
        _ => Definition.Description,
    };

    public void RaiseDescriptionChanged() => OnPropertyChanged(nameof(Description));

    public string Unit => Definition.Unit;
    public string? Address => Definition.Address;

    /// <summary>Hệ số Gain (1/10/100/1000) đọc từ spec-register-map.csv — hiển thị chỉ-đọc ở cột "PLC Gain"
    /// tại tab Set Spec., quyết định số chữ số thập phân làm tròn của Giới hạn/Giá trị đo (xem
    /// EolTester.Core.PlcGainScale).</summary>
    public int Scale => Definition.Scale;

    /// <summary>Giới hạn ĐANG HIỆU LỰC (đã "Xác nhận thay đổi" lần gần nhất) — dùng cho tab Main (hiển thị +
    /// tính Đạt/Không đạt qua <see cref="Passed"/>). KHÔNG đổi khi Admin/Operator đang gõ dở ở tab Set spec.,
    /// chỉ đổi khi <see cref="EditLowerLimit"/>/<see cref="EditUpperLimit"/> được chốt lại (xem
    /// SettingTabViewModel.ConfirmChangesAsync). Giá trị THỰC (đã quy đổi theo Scale), không phải số nguyên
    /// thanh ghi PLC — xem TestStepDefinition.LowerLimit.</summary>
    [ObservableProperty]
    private decimal? _lowerLimit;

    [ObservableProperty]
    private decimal? _upperLimit;

    /// <summary>Giá trị Admin/Operator ĐANG GÕ ở lưới Set spec. — tách riêng khỏi <see cref="LowerLimit"/> (giá
    /// trị đang hiệu lực) để tab Main không "nhảy số" ngay khi gõ, chỉ đổi sau khi bấm "Xác nhận thay đổi".</summary>
    [ObservableProperty]
    private decimal? _editLowerLimit;

    [ObservableProperty]
    private decimal? _editUpperLimit;

    /// <summary>Giá trị giới hạn đang thật sự nằm trong thanh ghi PLC (đọc từ PlcRegisterImage qua
    /// LowerLimitAddress/UpperLimitAddress mỗi tick polling) — dùng để so sánh với <see cref="EditLowerLimit"/>/
    /// <see cref="EditUpperLimit"/> và tô màu cảnh báo khi Admin/Operator đang gõ giá trị khác giá trị hiện có
    /// trong PLC (chưa "Xác nhận thay đổi"/chưa đẩy xuống).</summary>
    [ObservableProperty]
    private decimal? _registerLowerLimit;

    [ObservableProperty]
    private decimal? _registerUpperLimit;

    /// <summary>True nếu giá trị đang gõ ở Set spec. khác giá trị hiện có trong thanh ghi PLC — tô đỏ/cam để
    /// cảnh báo còn thay đổi chưa xác nhận/chưa đẩy xuống. Chỉ so sánh khi <see cref="RegisterLowerLimit"/>/
    /// <see cref="RegisterUpperLimit"/> ĐÃ CÓ giá trị thật đọc được từ PLC — lúc mới khởi động app (chưa kết
    /// nối/chưa tới tick polling đầu tiên) 2 giá trị này luôn null, và null != giá trị đã cấu hình sẽ luôn
    /// đúng, khiến MỌI dòng bị tô đỏ ngay cả khi Admin/Operator chưa đụng gì tới — bug thật đã gặp, hiểu sai
    /// thành "còn thay đổi chưa xác nhận" trong khi thực ra chỉ là chưa có dữ liệu PLC để so sánh.</summary>
    public bool IsLowerLimitPending => RegisterLowerLimit.HasValue && EditLowerLimit != RegisterLowerLimit;
    public bool IsUpperLimitPending => RegisterUpperLimit.HasValue && EditUpperLimit != RegisterUpperLimit;

    /// <summary>True nếu Giới hạn dưới đang gõ > Giới hạn trên đang gõ (cả 2 đều đã nhập) — dữ liệu vô lý,
    /// không thể dùng để so sánh Đạt/Không đạt lúc vận hành. Chặn "Xác nhận thay đổi" khi còn true — xem
    /// SettingTabViewModel.ConfirmChangesAsync.</summary>
    public bool IsLimitRangeInvalid =>
        EditLowerLimit.HasValue && EditUpperLimit.HasValue && EditLowerLimit > EditUpperLimit;

    /// <summary>True nếu giá trị đang gõ (sau khi quy đổi raw = value*Scale) nằm ngoài dải WordSigned
    /// (-32768..32767) — thanh ghi PLC không thể lưu được, chặn "Xác nhận thay đổi" khi còn true (giống
    /// <see cref="IsLimitRangeInvalid"/>) — xem SettingTabViewModel.ConfirmChangesAsync.</summary>
    public bool IsLowerLimitOutOfRange => IsRawOutOfRange(EditLowerLimit);
    public bool IsUpperLimitOutOfRange => IsRawOutOfRange(EditUpperLimit);

    private bool IsRawOutOfRange(decimal? value)
    {
        if (!value.HasValue) return false;
        var raw = value.Value * Definition.Scale;
        return raw < short.MinValue || raw > short.MaxValue;
    }

    [ObservableProperty]
    private decimal? _measuredValue;

    /// <summary>Trạng thái OK/NG đọc từ <see cref="TestStepDefinition.OkNgAddress"/> (1=OK/2=NG, null=chưa
    /// xác định) — dùng cho cột "OK / NG" và cho cột "Giá trị" của các bước không có giới hạn số (kiểu kiểm
    /// tra tín hiệu, VD "Kiểm tra LED 1"). Xem PlcPollingService.ReadOkNg.</summary>
    [ObservableProperty]
    private bool? _okNgResult;

    public bool IsConfigured => LowerLimit.HasValue || UpperLimit.HasValue;

    public bool? Passed
    {
        get
        {
            if (MeasuredValue is null || !IsConfigured) return null;
            if (LowerLimit.HasValue && MeasuredValue < LowerLimit) return false;
            if (UpperLimit.HasValue && MeasuredValue > UpperLimit) return false;
            return true;
        }
    }

    /// <summary>Chuỗi hiển thị chỉ-đọc cho tab Main — làm tròn đúng số chữ số thập phân theo Scale (VD
    /// Scale=100 luôn hiện đủ 2 chữ số kể cả "23.00", không rút gọn) theo đúng yêu cầu người dùng.</summary>
    public string LowerLimitDisplayText => FormatOrPlaceholder(LowerLimit, "-");
    public string UpperLimitDisplayText => FormatOrPlaceholder(UpperLimit, "-");
    public string MeasuredValueDisplayText => FormatOrPlaceholder(MeasuredValue, "--");

    private string FormatOrPlaceholder(decimal? value, string placeholder) =>
        value.HasValue
            ? value.Value.ToString("F" + PlcGainScale.DecimalDigits(Definition.Scale), CultureInfo.InvariantCulture)
            : placeholder;

    /// <summary>Ô nhập ở lưới Set Spec. — 2 chiều với <see cref="EditLowerLimit"/>/<see cref="EditUpperLimit"/>,
    /// tự làm tròn theo Scale ngay khi rời ô (xem EolTester.Core.PlcGainScale) và tự format lại đúng số chữ số
    /// thập phân sau khi làm tròn. Text rỗng/không parse được số = "chưa cấu hình" (null).</summary>
    public string EditLowerLimitText
    {
        get => FormatOrPlaceholder(EditLowerLimit, string.Empty);
        set => EditLowerLimit = ParseEditText(value);
    }

    public string EditUpperLimitText
    {
        get => FormatOrPlaceholder(EditUpperLimit, string.Empty);
        set => EditUpperLimit = ParseEditText(value);
    }

    private decimal? ParseEditText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? PlcGainScale.Round(parsed, Definition.Scale)
            : null;
    }

    partial void OnMeasuredValueChanged(decimal? value)
    {
        OnPropertyChanged(nameof(Passed));
        OnPropertyChanged(nameof(MeasuredValueDisplayText));
    }
    partial void OnLowerLimitChanged(decimal? value)
    {
        OnPropertyChanged(nameof(Passed));
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(LowerLimitDisplayText));
    }
    partial void OnUpperLimitChanged(decimal? value)
    {
        OnPropertyChanged(nameof(Passed));
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(UpperLimitDisplayText));
    }

    partial void OnEditLowerLimitChanged(decimal? value)
    {
        OnPropertyChanged(nameof(IsLowerLimitPending));
        OnPropertyChanged(nameof(IsLimitRangeInvalid));
        OnPropertyChanged(nameof(IsLowerLimitOutOfRange));
        OnPropertyChanged(nameof(EditLowerLimitText));
    }
    partial void OnEditUpperLimitChanged(decimal? value)
    {
        OnPropertyChanged(nameof(IsUpperLimitPending));
        OnPropertyChanged(nameof(IsLimitRangeInvalid));
        OnPropertyChanged(nameof(IsUpperLimitOutOfRange));
        OnPropertyChanged(nameof(EditUpperLimitText));
    }
    partial void OnRegisterLowerLimitChanged(decimal? value) => OnPropertyChanged(nameof(IsLowerLimitPending));
    partial void OnRegisterUpperLimitChanged(decimal? value) => OnPropertyChanged(nameof(IsUpperLimitPending));
}
