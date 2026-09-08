namespace EolTester.Configuration;

/// <summary>Nguồn ánh xạ Key (TestStepDefinition) -> Address (thanh ghi Dxxxx) cho các bước đo High/Low mode.</summary>
public interface ISpecRegisterMapSource
{
    Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken ct = default);

    /// <summary>Đọc cột "Scale" (hệ số Gain, xem EolTester.Core.PlcGainScale) theo Key — Key không có dòng
    /// trong CSV hoặc cột Scale rỗng/không hợp lệ (khác 1/10/100/1000) mặc định 1.</summary>
    Task<IReadOnlyDictionary<string, int>> LoadScalesAsync(CancellationToken ct = default);

    /// <summary>Đọc cột "Label1"/"Label2" (nhãn hiển thị VI/EN, tùy chọn) theo Key — hiện chỉ dùng cho
    /// PARAM_DELAY_TIMER_1..10 (nhãn "Timer Setting" ở tab Set Spec., xem App.xaml.cs). Key không có nhãn
    /// nào trong CSV thì không xuất hiện trong dictionary trả về (bên gọi giữ nguyên nhãn resx mặc định).</summary>
    Task<IReadOnlyDictionary<string, (string? Label1, string? Label2)>> LoadLabelsAsync(CancellationToken ct = default);

    /// <summary>Đọc cột "Heading" (tiêu đề cột khi xuất CSV kết quả, xem CsvResultExportService) theo Key —
    /// CHỈ trả về các Key có Heading không rỗng (dùng để lọc ComboBox gợi ý cột CSV ở tab Set Spec., ẩn hết các
    /// Key nội bộ CMD_/PARAM_/SIGNAL_ không có ý nghĩa xuất báo cáo).</summary>
    Task<IReadOnlyDictionary<string, string>> LoadHeadingsAsync(CancellationToken ct = default);

    /// <summary>Đọc cột "Type" (kiểu diễn giải giá trị khi xuất CSV kết quả — hiện chỉ có "OKNG", xem
    /// CsvResultExportService) theo Key — Key không có Type trong CSV thì không xuất hiện trong dictionary
    /// trả về (bên gọi coi như ghi giá trị thô, không diễn giải).</summary>
    Task<IReadOnlyDictionary<string, string>> LoadTypesAsync(CancellationToken ct = default);
}
