using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EolTester.App.Localization;
using EolTester.App.Services;
using EolTester.App.ViewModels.Rows;
using EolTester.Communication;
using EolTester.Configuration;
using EolTester.Configuration.Models;
using EolTester.Core;
using EolTester.Core.Enums;
using EolTester.Core.Models;
using EolTester.Data;
using EolTester.Security;
using Microsoft.Win32;
using Serilog;

namespace EolTester.App.ViewModels;

public sealed record ScanModeOption(BarcodeScanMode Value, string Label);

/// <summary>1 gợi ý trong ComboBox chọn nhanh cột CSV — chọn xong thì <see cref="InsertText"/> được nối vào
/// <see cref="SettingTabViewModel.CsvColumnsText"/>. 4 gợi ý đầu danh sách là token đặc biệt cố định
/// (&lt;STT&gt;/&lt;date&gt;/&lt;time&gt;/&lt;JOB&gt;), phần còn lại là địa chỉ thanh ghi PLC — chỉ những Key
/// có khai báo Heading trong spec-register-map.csv mới xuất hiện (xem <see cref="ISpecRegisterMapSource.LoadHeadingsAsync"/>).</summary>
public sealed record CsvColumnSuggestion(string DisplayText, string InsertText);

public partial class SettingTabViewModel : ObservableObject
{
    private readonly ISpecProfileStore _specStore;
    private readonly ITestParametersStore _paramStore;
    private readonly ICsvExportSettingsStore _csvExportStore;
    private readonly ISpecRegisterMapSource _registerMapSource;
    private readonly IAuditLogService _auditLog;
    private readonly IAuthenticationService _auth;
    private readonly MainTabViewModel _mainTab;
    private readonly Services.PlcPollingService _polling;

    public ObservableCollection<TestStepRowViewModel> HighModeSteps => _mainTab.HighModeSteps;
    public ObservableCollection<TestStepRowViewModel> LowModeSteps => _mainTab.LowModeSteps;

    /// <summary>
    /// Danh sách hiển thị/sửa được ở lưới Set spec. — loại các bước kiểu OK/NG thuần túy (VD "Kiểm tra LED
    /// 1/2": không có giới hạn số, chỉ đọc trạng thái OK/NG từ 1 thanh ghi cố định) vì chúng không có gì để
    /// Admin/Operator chỉnh (không LowerLimit/UpperLimit). <see cref="ConfirmChangesAsync"/> vẫn lưu TOÀN BỘ
    /// <see cref="HighModeSteps"/>/<see cref="LowModeSteps"/> (không lọc) để không làm mất các bước này khỏi
    /// spec-profile.json. Đánh giá 1 lần lúc bind (HighModeSteps/LowModeSteps không Add/Remove sau khi
    /// LoadActiveModelAsync chạy ở App.xaml.cs) nên không cần ObservableCollection riêng.
    /// </summary>
    /// <summary>Phải là <see cref="List{T}"/> (IList), KHÔNG được để nguyên IEnumerable lazy từ LINQ Where —
    /// DataGrid.ItemsSource bind vào IEnumerable-only tạo ra CollectionView không hỗ trợ IEditableCollectionView,
    /// khiến BeginEdit ném InvalidOperationException ở MỌI lần double-click/F2 vào ô (bị DispatcherUnhandledException
    /// nuốt âm thầm, không crash nhưng ô không bao giờ vào được chế độ edit — bug thật đã gặp, xem docs/session-log).</summary>
    public List<TestStepRowViewModel> EditableHighModeSteps => HighModeSteps.Where(s => s.IsConfigured).ToList();
    public List<TestStepRowViewModel> EditableLowModeSteps => LowModeSteps.Where(s => s.IsConfigured).ToList();

    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private bool _canEditSpec;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusMessageIsError;

    [ObservableProperty] private int _scanCodeLength = 12;
    [ObservableProperty] private int _serialStartIndex = 1;
    [ObservableProperty] private int _serialEndIndex = 8;
    [ObservableProperty] private bool _allowEditBarcode;

    /// <summary>PARAM_REQUIRE_SCAN_ORDER (D104.1). Option ĐỘC LẬP — SCAN MODE không còn ép/khóa (xem
    /// <see cref="ApplyScanModeInvariant"/>). Chỉ được đọc khi pipeline SCAN MODE chạy ở ShellViewModel.CommitScanAsync.</summary>
    [ObservableProperty] private bool _requireScanOrder;

    /// <summary>PARAM_CHECK_DUPLICATE_LOG (D104.5) — checkbox "Check Duplicate in log" cạnh "Bắt buộc đúng thứ
    /// tự tem". SCAN MODE: bật thì ShellViewModel.CommitScanAsync kiểm tra mã vừa quét đã có trong scanlog
    /// chống trùng của hôm nay chưa. Option ĐỘC LẬP với <see cref="RequireScanOrder"/>, SCAN MODE không ép/khóa.</summary>
    [ObservableProperty] private bool _checkDuplicateInLog = true;

    /// <summary>Khối "MODE TEST" — 2 nút High-&gt;Low/Low-&gt;High set trực tiếp (không toggle), tự lưu + đẩy
    /// PLC ngay khi đổi (PARAM_TEST_ORDER, D104.3, spec-register-map.csv). false = High-&gt;Low (0),
    /// true = Low-&gt;High (1). Nút khớp giá trị hiện tại tô xanh (BoolEqualsToBrushConverter, SettingTabView.xaml).</summary>
    [ObservableProperty] private bool _testOrderLowToHigh;

    /// <summary>2 nút SCAN MODE/REV MODE set trực tiếp (không toggle), cùng hàng với MODE TEST — tự lưu + đẩy
    /// PLC ngay khi đổi (PARAM_SCAN_REV_MODE, D104.4). false = SCAN MODE (0, mặc định): ép
    /// <see cref="AllowEditBarcode"/>=false + khóa checkbox đó, luồng quét barcode ở footer tự chạy pipeline
    /// kiểm tra rồi tự gửi CMD_START (xem ShellViewModel.CommitScanAsync). <see cref="RequireScanOrder"/> và
    /// <see cref="CheckDuplicateInLog"/> KHÔNG bị ép/khóa — 2 option độc lập. true = REV MODE (1): quay lại
    /// hành vi thủ công hiện có, không ép/khóa gì.</summary>
    [ObservableProperty] private bool _scanRevMode;

    /// <summary>Cách nhận diện điểm bắt đầu/kết thúc 1 lần quét barcode — đọc live bởi
    /// <see cref="Behaviors.KeyboardWedgeScanCapture"/> qua ShellViewModel.SettingTab, không có thanh ghi PLC
    /// (thuần logic phân giải dữ liệu quét ở phía PC).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrefixModeSelected))]
    private BarcodeScanMode _scanMode = BarcodeScanMode.PrefixStripped;

    [ObservableProperty] private string _scanPrefixText = "`";

    /// <summary>13 dòng bảng "Timer Setting (ms)": 3 thời gian cố định (test High/Low mode, giữ nút) xếp đầu,
    /// rồi tới 10 "Delay timer" tùy chỉnh — xây dựng đúng 1 lần trong <see cref="LoadParametersAsync"/>, KHÔNG
    /// rebuild theo model/ngôn ngữ (Label tự refresh qua TimerSettingRowViewModel.RaiseLabelChanged, xem
    /// language.LanguageChanged bên dưới) — giữ ổn định instance để không phá edit state của DataGrid.</summary>
    public List<TimerSettingRowViewModel> TimerSettingRows { get; private set; } = [];

    /// <summary>Danh sách (giá trị, nhãn đã dịch) cho ComboBox chọn ScanMode — KHÔNG dùng converter (xem
    /// CLAUDE.md mục 10: converter chỉ re-run khi giá trị bind đổi, không phải khi đổi ngôn ngữ, chữ sẽ "đứng
    /// hình" sai ngôn ngữ sau khi bấm cờ VI/EN). Property tính toán + subscribe LanguageChanged re-raise.</summary>
    public IReadOnlyList<ScanModeOption> ScanModeOptions => Enum.GetValues<BarcodeScanMode>()
        .Select(m => new ScanModeOption(m, GetScanModeLabel(m)))
        .ToList();

    private static string GetScanModeLabel(BarcodeScanMode mode) => mode switch
    {
        BarcodeScanMode.PrefixStripped => Translation.Instance["Setting_ScanModePrefixStripped"],
        BarcodeScanMode.PrefixKept => Translation.Instance["Setting_ScanModePrefixKept"],
        BarcodeScanMode.FixedLength => Translation.Instance["Setting_ScanModeFixedLength"],
        _ => mode.ToString(),
    };

    public bool IsPrefixModeSelected => ScanMode != BarcodeScanMode.FixedLength;

    [ObservableProperty] private string _csvOutputDirectory = string.Empty;

    /// <summary>Danh sách cột CSV tùy chỉnh, gõ trực tiếp cách nhau bằng dấu phẩy — gọn 1 dòng thay vì bảng
    /// nhiều dòng (từng bị che khuất do tràn khung canvas cố định, xem CLAUDE.md mục 12). Mỗi phần tử là 1 địa
    /// chỉ thanh ghi ("Dxxxx"/"Dxxxx.b", VD "D50") hoặc 1 trong 4 token đặc biệt &lt;STT&gt;/&lt;date&gt;/
    /// &lt;time&gt;/&lt;JOB&gt; (kể cả dạng composite ngày-giờ như "&lt;yyyy&gt;-&lt;mm&gt;-&lt;dd&gt;") —
    /// xem <see cref="CsvColumnSpecParser"/>. Cột Barcode luôn tự có mặt (không cần gõ), trừ khi tự gõ
    /// &lt;barcode&gt; để chỉ định vị trí khác. Header cột lấy từ Heading trong spec-register-map.csv (địa
    /// chỉ thanh ghi) hoặc nhãn cố định (token đặc biệt) — xem <see cref="CsvResultExportService"/>.</summary>
    [ObservableProperty] private string _csvColumnsText = string.Empty;

    /// <summary>Gợi ý nhanh (Key + Address) lấy từ spec-register-map.csv — chọn 1 mục sẽ tự nối Address vào
    /// <see cref="CsvColumnsText"/> thay vì bắt Admin nhớ/gõ chính xác toàn bộ địa chỉ.</summary>
    public List<CsvColumnSuggestion> CsvColumnSuggestions { get; private set; } = [];

    [ObservableProperty] private CsvColumnSuggestion? _selectedCsvColumnSuggestion;

    private readonly Dictionary<string, (decimal? Lower, decimal? Upper)> _snapshotBeforeEdit = [];

    /// <summary>Chặn auto-save/push tham số trong lúc <see cref="LoadParametersAsync"/> đang gán giá trị nạp
    /// từ file — nếu không, mỗi property set lúc nạp cũng kích hoạt 1 lần lưu/ghi PLC không cần thiết.</summary>
    private bool _suppressParamAutoSave;

    public SettingTabViewModel(
        ISpecProfileStore specStore,
        ITestParametersStore paramStore,
        ICsvExportSettingsStore csvExportStore,
        ISpecRegisterMapSource registerMapSource,
        IAuditLogService auditLog,
        IAuthenticationService auth,
        Services.ILanguageService language,
        MainTabViewModel mainTab,
        Services.PlcPollingService polling)
    {
        _specStore = specStore;
        _paramStore = paramStore;
        _csvExportStore = csvExportStore;
        _registerMapSource = registerMapSource;
        _auditLog = auditLog;
        _auth = auth;
        _mainTab = mainTab;
        _polling = polling;

        language.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ScanModeOptions));
            foreach (var row in TimerSettingRows) row.RaiseLabelChanged();
        };
    }

    /// <summary>Chọn 1 gợi ý trong ComboBox → nối Address của nó vào cuối <see cref="CsvColumnsText"/> (cách
    /// bằng ", " nếu ô đã có nội dung), rồi tự reset về null — để chọn LẠI đúng gợi ý đó ở lần kế tiếp vẫn kích
    /// hoạt được sự kiện này (WPF không raise SelectionChanged nếu SelectedItem không đổi).</summary>
    partial void OnSelectedCsvColumnSuggestionChanged(CsvColumnSuggestion? value)
    {
        if (value is null) return;
        CsvColumnsText = string.IsNullOrWhiteSpace(CsvColumnsText) ? value.InsertText : $"{CsvColumnsText}, {value.InsertText}";
        SelectedCsvColumnSuggestion = null;
    }

    /// <summary>
    /// Operator (đã đăng nhập) và Admin đều được sửa thông số Set Spec — riêng khối "XUẤT FILE CSV KẾT QUẢ"
    /// yêu cầu Admin (xem <c>IsEnabled="{Binding IsAdmin}"</c> ở SettingTabView.xaml).
    /// </summary>
    public void RefreshPermissions(bool canEditSpec, bool isAdmin)
    {
        CanEditSpec = canEditSpec;
        IsAdmin = isAdmin;
    }

    public async Task LoadParametersAsync()
    {
        _suppressParamAutoSave = true;
        try
        {
            var p = await _paramStore.LoadAsync();
            ScanCodeLength = p.ScanCodeLength;
            SerialStartIndex = p.SerialStartIndex;
            SerialEndIndex = p.SerialEndIndex;
            AllowEditBarcode = p.AllowEditBarcode;
            RequireScanOrder = p.RequireScanOrder;
            CheckDuplicateInLog = p.CheckDuplicateInLog;
            TestOrderLowToHigh = p.TestOrderLowToHigh;
            ScanRevMode = p.ScanRevMode;
            // Gọi tường minh (không dựa vào OnScanRevModeChanged) — [ObservableProperty] bỏ qua raise OnChanged
            // khi giá trị gán trùng giá trị mặc định hiện có của field (VD ScanRevMode=false lúc load trùng
            // default false của field), nên cascade ép AllowEditBarcode=false sẽ KHÔNG chạy nếu chỉ dựa vào
            // OnChanged — bug thật đã gặp (checkbox lẽ ra phải bị khóa/ép nhưng vẫn giữ giá trị persist cũ từ
            // trước khi có tính năng SCAN MODE).
            ApplyScanModeInvariant(ScanRevMode);
            ScanMode = p.ScanMode;
            ScanPrefixText = p.ScanPrefixText;

            if (TimerSettingRows.Count == 0)
            {
                TimerSettingRows = TimerSettingRowViewModel.Definitions
                    .Select(d => new TimerSettingRowViewModel(d.LabelKey, d.ParamKey))
                    .ToList();
                OnPropertyChanged(nameof(TimerSettingRows));
            }
            // 3 dòng đầu (High/Low duration, Button hold) là các trường cố định của TestParameters; 10 dòng
            // sau khớp với mảng DelayTimersMs theo đúng thứ tự — xem TimerSettingRowViewModel.Definitions.
            TimerSettingRows[0].ValueMs = p.HighModeDurationMs;
            TimerSettingRows[1].ValueMs = p.LowModeDurationMs;
            TimerSettingRows[2].ValueMs = p.ButtonHoldMs;
            for (var i = 0; i < 10; i++)
            {
                // 0 nghĩa là "chưa gán" (thiếu phần tử trong mảng CŨ, hoặc file config cũ lưu từ trước khi có
                // mặc định — xem TestParameters.DelayTimersMs) — tự nạp lại 5 (đơn vị x0.1s) thay vì giữ 0 mù mờ,
                // đồng thời tự "chữa lành" file cũ ngay lần lưu kế tiếp (PersistAndPushParametersAsync ghi lại
                // đúng giá trị đang hiển thị ở TimerSettingRows).
                var raw = p.DelayTimersMs.Length > i ? p.DelayTimersMs[i] : 0;
                TimerSettingRows[3 + i].ValueMs = raw == 0 ? 5 : raw;
            }
            foreach (var row in TimerSettingRows)
            {
                row.ValueCommitted -= OnTimerSettingCommitted;
                row.ValueCommitted += OnTimerSettingCommitted;
            }

            var registerMap = await _registerMapSource.LoadAsync();
            var headings = await _registerMapSource.LoadHeadingsAsync();
            var suggestions = new List<CsvColumnSuggestion>
            {
                new(Translation.Instance["Setting_CsvTokenStt"], "<STT>"),
                new(Translation.Instance["Setting_CsvTokenDate"], "<date>"),
                new(Translation.Instance["Setting_CsvTokenTime"], "<time>"),
                new(Translation.Instance["Setting_CsvTokenJob"], "<JOB>"),
            };
            suggestions.AddRange(headings
                .Where(kv => registerMap.ContainsKey(kv.Key))
                .OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new CsvColumnSuggestion($"{kv.Value} ({registerMap[kv.Key]})", registerMap[kv.Key])));
            CsvColumnSuggestions = suggestions;
            OnPropertyChanged(nameof(CsvColumnSuggestions));

            var csvSettings = await _csvExportStore.LoadAsync();
            // Luôn hiện đúng thư mục thật sự đang dùng (kể cả khi Admin chưa tự chọn — dùng mặc định, xem
            // AppPaths.ResolveCsvExportDirectory) — không để ô này trống trong khi CSV vẫn đang được ghi ở nơi
            // khác phía sau.
            CsvOutputDirectory = AppPaths.ResolveCsvExportDirectory(csvSettings.OutputDirectory);
            CsvColumnsText = string.Join(", ", csvSettings.ColumnSpecs);
        }
        finally
        {
            _suppressParamAutoSave = false;
        }

        // Đẩy ngay toàn bộ tham số vừa nạp xuống PLC (kể cả khi không có gì thay đổi so với file cũ) — các
        // thanh ghi PARAM_* là chỉ-ghi, PC không đọc ngược lại để xác nhận, nên đây là cách DUY NHẤT đảm bảo
        // PLC thật sự khớp với những gì UI đang hiển thị ngay từ lúc khởi động, không phải chờ người dùng sửa 1
        // tham số bất kỳ mới vô tình đồng bộ lại. Quan trọng nhất với các giá trị vừa được "chữa" từ 0 → 500
        // (DelayTimersMs) — nếu không đẩy ngay, UI hiện 500 nhưng PLC có thể vẫn đang giữ 0 (hoặc giá trị cũ từ
        // trước khi PLC bị mất điện/reset độc lập với PC) cho tới lần "Xác nhận thay đổi" kế tiếp — đúng kiểu
        // lệch hiển thị người dùng lo ngại.
        // CỐ Ý fire-and-forget (KHÔNG await) — đây gọi 18 lệnh ghi PLC tuần tự (5 tham số + 13 dòng Timer
        // Setting), mỗi lệnh có thể mất tới ~TimeoutMs×(RetryCount+1) (mặc định ~4s) nếu PLC không phản hồi —
        // tổng cộng có thể tới ~72s. BUG THẬT đã gặp: bản đầu dùng `await` ngay trong LoadParametersAsync(),
        // hàm này chạy TRƯỚC MainWindow.Show() trong App.xaml.cs — khi không có PLC thật trả lời (COM sai/PLC
        // chưa đấu), app phải "chờ" gần 1 phút rưỡi mới hiện cửa sổ, trông y hệt app bị treo/không khởi động
        // được. Đổi sang fire-and-forget: MainWindow hiện ngay lập tức, việc đẩy PLC chạy nền phía sau, dùng bản
        // an toàn (nuốt exception + log) vì không còn nằm trong luồng khởi động được await nữa.
        _ = PersistAndPushParametersSafeAsync();
    }

    /// <summary>
    /// "Độ dài mã scan / bắt buộc Scan / bắt buộc đúng thứ tự tem" lưu file + đẩy xuống PLC NGAY khi người
    /// dùng đổi giá trị — không cần bấm "Xác nhận thay đổi" (khác với giới hạn High/Low mode ở lưới Set
    /// spec., vẫn chỉ ghi lúc bấm Confirm — xem ConfirmChangesAsync). SerialStartIndex/SerialEndIndex/
    /// ScanMode/ScanPrefixText không có thanh ghi PLC tương ứng (SerialStartIndex/SerialEndIndex phụ thuộc
    /// field khác trigger lưu kèm; ScanMode/ScanPrefixText có trigger auto-save file riêng — thuần logic
    /// phân giải dữ liệu quét ở phía PC, không cần PLC biết). Thời gian test High/Low/giữ nút giờ nằm trong
    /// <see cref="TimerSettingRows"/> (bảng "Timer Setting"), tự lưu qua <see cref="OnTimerSettingCommitted"/>.
    /// </summary>
    partial void OnScanCodeLengthChanged(int value) => TriggerParamAutoSave();
    partial void OnAllowEditBarcodeChanged(bool value) => TriggerParamAutoSave();
    partial void OnRequireScanOrderChanged(bool value) => TriggerParamAutoSave();
    partial void OnCheckDuplicateInLogChanged(bool value) => TriggerParamAutoSave();
    partial void OnTestOrderLowToHighChanged(bool value) => TriggerParamAutoSave();
    partial void OnScanModeChanged(BarcodeScanMode value) => TriggerParamAutoSave();
    partial void OnScanPrefixTextChanged(string value) => TriggerParamAutoSave();

    /// <summary>Chuyển VÀO SCAN MODE (false) tự ép "Cho phép sửa Barcode"=false (checkbox đó bị khóa ở UI khi
    /// ScanRevMode=false, xem SettingTabView.xaml) — property tự có OnAllowEditBarcodeChanged nên gán ở đây tự
    /// kích hoạt lưu+đẩy PLC, không cần gọi tay. "Bắt buộc đúng thứ tự tem" và "Check Duplicate in log" KHÔNG
    /// còn bị ép/khóa (từ 2026-08-28) — là 2 option độc lập. Chuyển sang REV MODE (true) KHÔNG đụng gì tới các
    /// giá trị đó — REV MODE không ép/tự động gì.</summary>
    partial void OnScanRevModeChanged(bool value)
    {
        ApplyScanModeInvariant(value);
        TriggerParamAutoSave();
    }

    private void ApplyScanModeInvariant(bool scanRevMode)
    {
        if (!scanRevMode)
        {
            AllowEditBarcode = false;
        }
    }

    private void OnTimerSettingCommitted(TimerSettingRowViewModel row) => TriggerParamAutoSave();

    private void TriggerParamAutoSave()
    {
        if (_suppressParamAutoSave) return;
        _ = PersistAndPushParametersSafeAsync();
    }

    private async Task PersistAndPushParametersSafeAsync()
    {
        try
        {
            await PersistAndPushParametersAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Không thể tự động lưu/ghi tham số cài đặt");
        }
    }

    /// <summary>Lưu TestParameters vào file + đẩy các tham số có thanh ghi tương ứng xuống PLC (spec-register-map.csv)
    /// — dùng chung cho auto-save theo từng thay đổi lẫn nút "Xác nhận thay đổi".</summary>
    private async Task PersistAndPushParametersAsync()
    {
        await _paramStore.SaveAsync(new TestParameters
        {
            ScanCodeLength = ScanCodeLength,
            HighModeDurationMs = TimerSettingRows[0].ValueMs,
            LowModeDurationMs = TimerSettingRows[1].ValueMs,
            ButtonHoldMs = TimerSettingRows[2].ValueMs,
            SerialStartIndex = SerialStartIndex,
            SerialEndIndex = SerialEndIndex,
            AllowEditBarcode = AllowEditBarcode,
            RequireScanOrder = RequireScanOrder,
            CheckDuplicateInLog = CheckDuplicateInLog,
            TestOrderLowToHigh = TestOrderLowToHigh,
            ScanRevMode = ScanRevMode,
            ScanMode = ScanMode,
            ScanPrefixText = ScanPrefixText,
            DelayTimersMs = TimerSettingRows.Skip(3).Select(r => r.ValueMs).ToArray(),
        });

        await _polling.WriteParamWordAsync("PARAM_SCAN_CODE_LENGTH", ScanCodeLength);
        await _polling.WriteParamBitAsync("PARAM_EDIT_SCAN", AllowEditBarcode);
        await _polling.WriteParamBitAsync("PARAM_REQUIRE_SCAN_ORDER", RequireScanOrder);
        await _polling.WriteParamBitAsync("PARAM_CHECK_DUPLICATE_LOG", CheckDuplicateInLog);
        await _polling.WriteParamBitAsync("PARAM_TEST_ORDER", TestOrderLowToHigh);
        await _polling.WriteParamBitAsync("PARAM_SCAN_REV_MODE", ScanRevMode);

        foreach (var row in TimerSettingRows)
        {
            await _polling.WriteParamWordAsync(row.ParamKey, row.ValueMs);
        }
    }

    public void SnapshotSteps()
    {
        _snapshotBeforeEdit.Clear();
        foreach (var step in HighModeSteps.Concat(LowModeSteps))
        {
            _snapshotBeforeEdit[step.Definition.Key] = (step.LowerLimit, step.UpperLimit);
        }
    }

    [RelayCommand]
    private async Task ConfirmChangesAsync()
    {
        if (!CanEditSpec) return;

        // Chặn xác nhận nếu còn bước nào có giá trị ngoài dải thanh ghi WordSigned (-32768..32767) — PLC không
        // thể lưu được. Kiểm tra trước "Lower > Upper" vì đây là lỗi nền tảng hơn (giá trị tự nó vô nghĩa,
        // không liên quan tới việc so sánh với giới hạn kia).
        var outOfRangeSteps = HighModeSteps.Concat(LowModeSteps)
            .Where(s => s.IsLowerLimitOutOfRange || s.IsUpperLimitOutOfRange).ToList();
        if (outOfRangeSteps.Count > 0)
        {
            StatusMessageIsError = true;
            StatusMessage = string.Format(Translation.Instance["Setting_LimitOutOfRange"],
                string.Join(", ", outOfRangeSteps.Select(s => s.Description)));
            return;
        }

        // Chặn xác nhận nếu còn bước nào có Giới hạn dưới > Giới hạn trên — dữ liệu vô lý, không thể dùng để
        // so sánh Đạt/Không đạt lúc vận hành. Không lưu file/không ghi PLC/không audit log khi bị chặn.
        var invalidSteps = HighModeSteps.Concat(LowModeSteps).Where(s => s.IsLimitRangeInvalid).ToList();
        if (invalidSteps.Count > 0)
        {
            StatusMessageIsError = true;
            StatusMessage = string.Format(Translation.Instance["Setting_InvalidLimitRange"],
                string.Join(", ", invalidSteps.Select(s => s.Description)));
            return;
        }
        StatusMessageIsError = false;

        var userName = _auth.CurrentUser?.UserName ?? "?";

        foreach (var step in HighModeSteps.Concat(LowModeSteps))
        {
            // Chốt giá trị đang gõ (Edit*) thành giá trị đang hiệu lực (LowerLimit/UpperLimit) — chỉ lúc này
            // tab Main mới "thấy" thay đổi (xem TestStepRowViewModel.LowerLimit/EditLowerLimit).
            step.LowerLimit = step.EditLowerLimit;
            step.UpperLimit = step.EditUpperLimit;
            step.Definition.LowerLimit = step.LowerLimit;
            step.Definition.UpperLimit = step.UpperLimit;

            if (_snapshotBeforeEdit.TryGetValue(step.Definition.Key, out var before) &&
                (before.Lower != step.LowerLimit || before.Upper != step.UpperLimit))
            {
                await _auditLog.LogAsync(userName, $"Đổi giới hạn {step.Definition.Key}",
                    oldValue: $"[{before.Lower};{before.Upper}]",
                    newValue: $"[{step.LowerLimit};{step.UpperLimit}]");
            }
        }

        var allSteps = HighModeSteps.Concat(LowModeSteps).Select(s => s.Definition).ToList();
        if (allSteps.Count > 0)
        {
            await _specStore.SaveAsync(new SpecProfile { Model = _mainTab.ActiveModel, Steps = allSteps });
        }

        // Giới hạn High/Low mode CHỈ ghi xuống PLC đúng lúc bấm "Xác nhận thay đổi" — KHÔNG realtime theo
        // từng ký tự gõ (khác các tham số cài đặt khác, đã tự lưu/ghi ngay khi đổi — xem TriggerParamAutoSave).
        // Ghi vào thanh ghi riêng của từng bước (LowerLimitAddress/UpperLimitAddress, spec-register-map.csv).
        await _polling.WriteStepLimitsAsync(allSteps);
        await PersistAndPushParametersAsync();

        SnapshotSteps();
        StatusMessage = string.Format(Translation.Instance["Setting_SavedAt"], DateTime.Now.ToString("HH:mm:ss"));
    }

    /// <summary>Set trực tiếp thứ tự test High/Low (không toggle) — "HighToLow" hoặc "LowToHigh". Tham số
    /// dạng string (không phải bool) vì CommandParameter từ XAML luôn là chuỗi literal; RelayCommand&lt;bool&gt;
    /// sẽ ném InvalidCastException khi ép kiểu trực tiếp — cùng lý do MonitorTabViewModel.SetModeAsync dùng
    /// string cho CMD_MODE Auto/Manual.</summary>
    [RelayCommand]
    private void SetTestOrder(string? order) => TestOrderLowToHigh = order == "LowToHigh";

    /// <summary>Set trực tiếp SCAN MODE/REV MODE (không toggle) — "Scan" hoặc "Rev", cùng lý do dùng string
    /// thay vì bool như <see cref="SetTestOrder"/>.</summary>
    [RelayCommand]
    private void SetScanRevMode(string? mode) => ScanRevMode = mode == "Rev";

    [RelayCommand]
    private void BrowseCsvOutputDirectory()
    {
        if (!IsAdmin) return;

        var dialog = new OpenFolderDialog { Title = Translation.Instance["Setting_CsvOutputDirectory"] };
        if (!string.IsNullOrWhiteSpace(CsvOutputDirectory)) dialog.InitialDirectory = CsvOutputDirectory;
        if (dialog.ShowDialog() == true) CsvOutputDirectory = dialog.FolderName;
    }

    /// <summary>
    /// Parse + validate <see cref="CsvColumnsText"/> (danh sách token cách nhau bằng dấu phẩy) rồi lưu xuống
    /// csv-export-settings.json. Mỗi token phải là 1 trong 4 token đặc biệt (&lt;STT&gt;/&lt;barcode&gt;/
    /// &lt;JOB&gt;/&lt;date&gt;/&lt;time&gt;/composite ngày-giờ, xem <see cref="CsvColumnSpecParser"/> — luôn
    /// hợp lệ, không cần khớp gì thêm) HOẶC 1 địa chỉ thanh ghi ("Dxxxx"/"Dxxxx.b") khớp đúng 1 Key **có khai
    /// báo Heading** trong spec-register-map.csv — đã chốt với người dùng: không cho lưu địa chỉ "lạ"/Key
    /// không có Heading (không có ý nghĩa xuất báo cáo), để tránh gõ nhầm số D không tồn tại mà không hay biết.
    /// </summary>
    [RelayCommand]
    private async Task SaveCsvExportSettingsAsync()
    {
        if (!IsAdmin) return;

        var tokens = CsvColumnsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var knownAddresses = CsvColumnSuggestions
            .Where(s => ModbusWordAddress.TryParse(s.InsertText, out _))
            .Select(s => ModbusWordAddress.Parse(s.InsertText))
            .ToHashSet();

        var invalidTokens = new List<string>();
        var specs = new List<string>();
        foreach (var token in tokens)
        {
            if (CsvColumnSpecParser.Parse(token) is CsvColumnSpec.RegisterColumn register &&
                (!ModbusWordAddress.TryParse(register.RawText, out var parsed) || !knownAddresses.Contains(parsed)))
            {
                invalidTokens.Add(token);
                continue;
            }
            specs.Add(token.Trim());
        }

        if (invalidTokens.Count > 0)
        {
            StatusMessageIsError = true;
            StatusMessage = string.Format(Translation.Instance["Setting_CsvInvalidAddress"], string.Join(", ", invalidTokens));
            return;
        }

        await _csvExportStore.SaveAsync(new CsvExportSettings { OutputDirectory = CsvOutputDirectory, ColumnSpecs = specs });

        var userName = _auth.CurrentUser?.UserName ?? "?";
        await _auditLog.LogAsync(userName, "Cấu hình xuất CSV", newValue: $"{CsvOutputDirectory}; cột: {string.Join(", ", specs)}");

        StatusMessageIsError = false;
        StatusMessage = string.Format(Translation.Instance["Setting_SavedAt"], DateTime.Now.ToString("HH:mm:ss"));
    }
}
