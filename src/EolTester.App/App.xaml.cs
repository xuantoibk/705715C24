using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using EolTester.App.Services;
using EolTester.App.ViewModels;
using EolTester.Communication;
using EolTester.Communication.Mc;
using EolTester.Communication.ModbusRtu;
using EolTester.Communication.Slmp;
using EolTester.Configuration;
using EolTester.Configuration.Models;
using EolTester.Data;
using EolTester.Security;
using EolTester.Security.Licensing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EolTester.App;

public partial class App : Application
{
    private IHost? _host;
    private IPlcSlaveService? _activeSlaveService;

    // Chỉ 1 tiến trình được giữ cổng COM/kết nối PLC tại 1 thời điểm — 2 instance cùng chạy sẽ tranh nhau
    // COM4 (UnauthorizedAccessException "Access denied", bug thật đã gặp khi debug/double-click nhầm exe).
    // GUID cố định, không đổi giữa các lần build — đổi sẽ khiến instance cũ/mới không nhận ra nhau.
    private const string SingleInstanceMutexName = "EolTester705715_SingleInstance_7C2A9E3B-4F1D-4B8A-9E5C-2D6F8A1B3C7E";
    private Mutex? _singleInstanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out var isFirstInstance);
        if (!isFirstInstance)
        {
            new SingleInstanceWarningWindow().ShowDialog();
            Shutdown();
            return;
        }

        // Lưới an toàn cuối cùng — [RelayCommand] sinh ICommand.Execute dạng "async void", nên 1 exception chưa
        // lường hết từ nút bấm sẽ crash cả app nếu không có handler ở đây (bug đã xảy ra thật).
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Tránh WPF tự Shutdown khi đóng LicenseWindow (ShutdownMode mặc định OnLastWindowClose sẽ coi
        // LicenseWindow là "cửa sổ cuối cùng" vì MainWindow chưa Show() lúc này) — tự gọi Shutdown() khi cần.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Bọc toàn bộ trình tự khởi động (nạp cấu hình JSON/CSV, dựng DI, kết nối PLC, tạo MainWindow) trong
        // 1 try/catch — bất kỳ exception nào ném ra TRƯỚC mainWindow.Show() (VD connection.json hỏng, cổng COM
        // ghi trong connection.json không tồn tại trên máy này, Protocol không hỗ trợ, DI lỗi) trước đây làm
        // app "chết câm lặng": không cửa sổ, không dialog, chỉ 1 dòng trong log — chỉ phát hiện được khi chạy
        // `dotnet run` ở foreground. Giờ chuyển thành 1 MessageBox lỗi rõ ràng rồi thoát có kiểm soát.
        try
        {
            await RunStartupSequenceAsync();
        }
        catch (Exception ex)
        {
            HandleFatalStartupError(ex);
        }
    }

    private async Task RunStartupSequenceAsync()
    {
        Directory.CreateDirectory(AppPaths.ConfigDirectory);
        Directory.CreateDirectory(AppPaths.LogsDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(AppPaths.LogFilePath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("Thư mục cấu hình/log: {Root} ({Kind})", AppPaths.RootDirectory,
            AppPaths.UsingExeRelativeRoot ? "cạnh exe" : "%LocalAppData% (exe không ghi được)");

        if (!EnsureSeedFiles()) return;

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        // Nạp ngôn ngữ đã lưu TRƯỚC khi tạo bất kỳ Window nào (kể cả LicenseWindow) để mọi cửa sổ
        // hiện đúng ngôn ngữ đã chọn từ lần chạy trước.
        var languageService = _host.Services.GetRequiredService<ILanguageService>();
        await languageService.InitializeAsync();

        // Tiêu đề header + tên Model mặc định đọc từ SeedData/spec-Default.csv (đổi tên sản phẩm chỉ cần
        // sửa CSV rồi build lại, không dò chuỗi hardcode rải rác) — nạp sớm, trước cả LicenseWindow, để mọi
        // cửa sổ hiện đúng ngay từ đầu. Thiếu file/khóa thì rơi về mặc định biên dịch sẵn trong code/resx.
        var defaultInfoSource = _host.Services.GetRequiredService<IDefaultProjectInfoSource>();
        var defaultInfo = await defaultInfoSource.LoadAsync();
        var defaultModel = defaultInfo.GetValueOrDefault("Model", "705/715");
        if (defaultInfo.TryGetValue("Header_Title.vi", out var headerTitleVi))
            Localization.Translation.Instance.SetOverride("vi-VN", "Header_Title", headerTitleVi);
        if (defaultInfo.TryGetValue("Header_Title.en", out var headerTitleEn))
            Localization.Translation.Instance.SetOverride("en-US", "Header_Title", headerTitleEn);

        // Tên các dòng bảng "Timer Setting" (tab Set Spec. — 3 thời gian cố định High/Low/giữ nút + 10 Delay
        // timer) — nhãn đọc từ cột Label1/Label2 của spec-register-map.csv (đi kèm luôn địa chỉ thanh ghi
        // trong cùng 1 file), cùng cơ chế ghi đè resx như Header_Title ở trên. Danh sách (LabelKey,ParamKey)
        // dùng chung với SettingTabViewModel.TimerSettingRows — xem TimerSettingRowViewModel.Definitions.
        var registerMapSource = _host.Services.GetRequiredService<ISpecRegisterMapSource>();
        var registerLabels = await registerMapSource.LoadLabelsAsync();
        foreach (var (resxKey, paramKey) in ViewModels.Rows.TimerSettingRowViewModel.Definitions)
        {
            if (!registerLabels.TryGetValue(paramKey, out var labels)) continue;

            if (labels.Label1 is not null)
                Localization.Translation.Instance.SetOverride("vi-VN", resxKey, labels.Label1);
            if (labels.Label2 is not null)
                Localization.Translation.Instance.SetOverride("en-US", resxKey, labels.Label2);
        }

        var licenseService = _host.Services.GetRequiredService<ILicenseService>();
        var licenseStatus = licenseService.CheckLicense();
        if (!licenseStatus.IsLicensed)
        {
            var licenseViewModel = _host.Services.GetRequiredService<LicenseViewModel>();
            licenseViewModel.LoadCurrentCode(licenseStatus.RegistrationCode);
            var licenseWindow = new LicenseWindow(licenseViewModel);
            bool? registered = licenseWindow.ShowDialog();
            if (registered != true)
            {
                Shutdown();
                return;
            }
        }

        await _host.StartAsync();

        // Chỉ 1 trong 2 vai trò chạy tại 1 thời điểm — RS-485/Modbus RTU chỉ cho phép 1 master trên bus.
        // Đổi Role ở tab Setup yêu cầu khởi động lại app để áp dụng (chưa hỗ trợ hot-swap runtime).
        var connectionSettingsStore = _host.Services.GetRequiredService<IConnectionSettingsStore>();
        var connectionSettings = await connectionSettingsStore.LoadAsync();
        var pollingService = _host.Services.GetRequiredService<PlcPollingService>();
        // Resolve sớm để nhánh Slave bên dưới có thể gắn badge trạng thái kết nối vào đúng service đang chạy
        // (PlcPollingService không polling ở Role=Slave nên State của nó không phản ánh gì).
        var shell = _host.Services.GetRequiredService<ShellViewModel>();
        if (connectionSettings.Role == ModbusRole.Master)
        {
            await pollingService.StartAsync();
        }
        else
        {
            // Vẫn nạp danh sách I/O để tab Monitor hiển thị được, chỉ không polling chủ động.
            await pollingService.LoadIoMapOnlyAsync();
            // Không có PollLoopAsync (chỉ chạy khi Role=Master) nên cần vòng lặp riêng đọc lại PlcRegisterImage
            // định kỳ để cập nhật Inputs/Outputs/LED/giá trị đo ra UI — nếu không, dữ liệu từ IPlcSlaveService
            // nhận từ PLC Master thật (hoặc Admin gõ tay qua "Giám sát DATA") chỉ nằm im trong cache, không hiển thị.
            await pollingService.StartUiRefreshOnlyAsync();

            // Chọn đúng cài đặt Slave theo Protocol đã cấu hình (Modbus RTU/MC Protocol/SLMP) qua factory —
            // driver Master và Slave dùng chung nguyên tắc chọn theo Protocol (xem CLAUDE.md mục 6).
            var slaveServiceFactory = _host.Services.GetRequiredService<IPlcSlaveServiceFactory>();
            _activeSlaveService = slaveServiceFactory.GetSlaveService(connectionSettings.Protocol);
            // Subscribe TRƯỚC khi StartAsync() để không bỏ lỡ Connecting -> Connected/Error do StartAsync tự set.
            shell.ObserveSlaveConnectionState(_activeSlaveService);
            shell.MonitorTab.ObserveSlaveConnectionState(_activeSlaveService);
            try
            {
                switch (connectionSettings.Protocol)
                {
                    case ProtocolType.ModbusRtu:
                        ((ModbusSlaveService)_activeSlaveService).Configure(
                            connectionSettings.ComPort,
                            connectionSettings.BaudRate,
                            connectionSettings.Parity.ToSerialPortParity(),
                            connectionSettings.DataBits,
                            connectionSettings.StopBits.ToSerialPortStopBits(),
                            (byte)connectionSettings.MySlaveId);
                        break;
                    case ProtocolType.McProtocol:
                        ((McProtocolSlaveService)_activeSlaveService).Configure(connectionSettings.IpAddress, connectionSettings.Port, connectionSettings.McFrameFormat.ToMcFrameFormatKind());
                        break;
                    case ProtocolType.Slmp:
                        ((SlmpSlaveService)_activeSlaveService).Configure(connectionSettings.IpAddress, connectionSettings.Port);
                        break;
                }

                await _activeSlaveService.StartAsync();
            }
            catch (Exception ex)
            {
                // Không có cổng COM/IP:Port thật (chưa đấu nối PLC) — ghi log, để app vẫn mở được bình thường
                // thay vì crash lúc khởi động; chỉ báo trạng thái kết nối sẽ hiện "Lỗi kết nối" cho operator.
                Log.Error(ex, "Không thể khởi động {SlaveService} (giao thức {Protocol})", _activeSlaveService.GetType().Name, connectionSettings.Protocol);
            }
        }

        // Nạp lại trạng thái runtime đã lưu (LastScanOk + số serial gần nhất + ảnh chụp D100-D199) TRƯỚC khi
        // các store chuyên biệt (spec-profile.json qua setupTab/settingTab) nạp+đẩy — store chuyên biệt vẫn
        // thắng cho phần chúng quản lý; runtime-state chỉ là lưới an toàn cho tắt không đúng quy trình.
        await shell.RestoreRuntimeStateAsync();

        var mainTab = _host.Services.GetRequiredService<MainTabViewModel>();
        await mainTab.LoadActiveModelAsync(defaultModel);

        var setupTab = _host.Services.GetRequiredService<SetupTabViewModel>();
        await setupTab.LoadAsync();

        var settingTab = _host.Services.GetRequiredService<SettingTabViewModel>();
        await settingTab.LoadParametersAsync();
        settingTab.SnapshotSteps();

        shell.Model = defaultModel;

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Title = $"EOL Tester - {defaultModel}";
        mainWindow.Closed += (_, _) => Shutdown();
        mainWindow.Show();
    }

    /// <summary>
    /// Kiểm tra 4 file seed trong <c>SeedData\</c> cạnh exe. Thiếu file nào → hiện dialog OK/Cancel:
    /// OK = khôi phục các file đó từ bản gốc nhúng trong assembly rồi chạy tiếp; Cancel = thoát ứng dụng.
    /// Nếu khôi phục ra đĩa thất bại (thư mục chỉ-đọc) → vẫn chạy được (các nguồn CSV tự đọc thẳng resource
    /// nhúng, xem SeedFiles), chỉ báo cho người dùng biết. Trả về <c>false</c> nếu người dùng chọn thoát.
    /// </summary>
    private bool EnsureSeedFiles()
    {
        var missing = SeedFiles.FindMissing(AppPaths.SeedDataDirectory);
        if (missing.Count == 0) return true;

        var list = "  " + string.Join("\n  ", missing);
        var choice = MessageBox.Show(
            "Thiếu file cấu hình đi kèm bản build (SeedData):\n" + list + "\n\n" +
            "Missing bundled configuration files.\n\n" +
            "OK / Đồng ý: khôi phục về mặc định rồi chạy tiếp.\n" +
            "Cancel / Thoát: thoát ứng dụng.",
            "EOL Tester 705/715",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (choice != MessageBoxResult.OK)
        {
            Log.Warning("Người dùng chọn thoát khi thiếu file seed: {Missing}", string.Join(", ", missing));
            Shutdown();
            return false;
        }

        var failed = SeedFiles.RestoreMissing(AppPaths.SeedDataDirectory, missing);
        var restored = missing.Where(m => !failed.Contains(m)).ToList();
        Log.Warning("Khôi phục file seed — đã ghi lại: [{Restored}]; không ghi được: [{Failed}]",
            string.Join(", ", restored), failed.Count == 0 ? "" : string.Join(", ", failed));

        if (failed.Count > 0)
        {
            MessageBox.Show(
                "Không ghi lại được vào SeedData (có thể do quyền thư mục):\n  " + string.Join("\n  ", failed) + "\n\n" +
                "Ứng dụng vẫn chạy được với nội dung mặc định nạp sẵn trong bộ nhớ.",
                "EOL Tester 705/715",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return true;
    }

    private void HandleFatalStartupError(Exception ex)
    {
        try
        {
            Log.Error(ex, "Lỗi nghiêm trọng khi khởi động — không mở được cửa sổ chính");
        }
        catch
        {
            // Serilog có thể chưa kịp cấu hình nếu lỗi xảy ra rất sớm — vẫn hiện MessageBox bên dưới.
        }

        MessageBox.Show(
            $"Ứng dụng không khởi động được.\nApplication failed to start.\n\n{ex.GetType().Name}: {ex.Message}\n\nChi tiết trong log:\n{AppPaths.LogFilePath}",
            "EOL Tester 705/715",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Shutdown();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Lỗi không xử lý được trên UI thread — chặn lại để không crash app");
        e.Handled = true;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ModbusRtuDriver>();
        services.AddSingleton<McProtocolDriver>();
        services.AddSingleton<SlmpDriver>();
        services.AddSingleton<IPlcCommunicationDriverFactory, PlcCommunicationDriverFactory>();

        services.AddSingleton<PlcRegisterImage>();
        services.AddSingleton<ModbusSlaveService>();
        services.AddSingleton<McProtocolSlaveService>();
        services.AddSingleton<SlmpSlaveService>();
        services.AddSingleton<IPlcSlaveServiceFactory, PlcSlaveServiceFactory>();
        services.AddSingleton<IIoMapStore>(sp => new JsonIoMapStore(AppPaths.ConfigDirectory, sp.GetRequiredService<IIoMapSeedSource>()));
        services.AddSingleton<ISpecProfileStore>(sp => new JsonSpecProfileStore(AppPaths.ConfigDirectory, sp.GetRequiredService<ISpecRegisterMapSource>()));
        services.AddSingleton<IConnectionSettingsStore>(_ => new JsonConnectionSettingsStore(AppPaths.ConfigDirectory));
        services.AddSingleton<IRegisterWatchStore>(_ => new JsonRegisterWatchStore(AppPaths.ConfigDirectory));
        services.AddSingleton<ITestParametersStore>(_ => new JsonTestParametersStore(AppPaths.ConfigDirectory));
        services.AddSingleton<IRuntimeStateStore>(_ => new JsonRuntimeStateStore(AppPaths.ConfigDirectory));
        services.AddSingleton<ICsvExportSettingsStore>(_ => new JsonCsvExportSettingsStore(AppPaths.ConfigDirectory));
        services.AddSingleton<ICsvResultExportService, CsvResultExportService>();
        services.AddSingleton<IBarcodeScanLogService>(_ => new BarcodeScanLogService(AppPaths.ScanLogDirectory));
        services.AddSingleton<IAuditLogService>(_ => new SqliteAuditLogService(AppPaths.AuditLogDatabasePath));
        services.AddSingleton<IUserStore>(_ => new FileUserStore(
            AppPaths.ConfigDirectory,
            Path.Combine(AppContext.BaseDirectory, "SeedData", "users-seed.csv")));
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<ILicenseStore>(_ => new RegistryLicenseStore());
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddTransient<LicenseViewModel>();

        services.AddSingleton<ILanguagePreferenceStore>(_ => new JsonLanguagePreferenceStore(AppPaths.ConfigDirectory));
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<IIoLabelCsvService, CsvIoLabelService>();
        services.AddSingleton<ISpecRegisterMapSource, CsvSpecRegisterMapSource>();
        services.AddSingleton<IIoMapSeedSource, CsvIoMapSeedSource>();
        services.AddSingleton<IDefaultProjectInfoSource, CsvDefaultProjectInfoSource>();

        services.AddSingleton(_ => Dispatcher.CurrentDispatcher);
        services.AddSingleton<PlcPollingService>();

        services.AddSingleton<MainTabViewModel>();
        services.AddSingleton<MonitorTabViewModel>();
        services.AddSingleton<SetupTabViewModel>();
        services.AddSingleton<SettingTabViewModel>();
        services.AddSingleton<ShellViewModel>();

        services.AddSingleton<MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var pollingService = _host.Services.GetRequiredService<PlcPollingService>();
            await pollingService.DisposeAsync();
            if (_activeSlaveService is not null) await _activeSlaveService.DisposeAsync();
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();

        // ReleaseMutex ném exception nếu instance này chưa từng chiếm được mutex (trường hợp !isFirstInstance,
        // Shutdown() gọi sớm trước khi tới đây) — chỉ instance đầu tiên mới thực sự sở hữu để giải phóng.
        if (_singleInstanceMutex is not null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); } catch (ApplicationException) { /* không sở hữu mutex — bỏ qua */ }
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
