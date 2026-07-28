using System.Windows;
using WinBridge.Services;
using WinBridge.ViewModels;
using WinBridge.Views;

namespace WinBridge;

public partial class App : Application
{
    private LoggingService? _logger;
    private SingleInstanceService? _singleInstance;
    private bool _allowWindowClose;
    private bool _isSavingOnClose;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsFirstInstance)
        {
            _singleInstance.SignalExistingInstance();
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.Error("UIで予期しないエラーが発生しました。", args.Exception);
            MessageBox.Show(L.T("予期しない問題が発生しました。アプリを続行できる場合があります。"),
                "WinBridge", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _logger = new LoggingService();
        _logger.Info("アプリを起動しました。");
        var settingsService = new AppSettingsService(_logger);
        var settings = await settingsService.LoadAsync();
        LocalizationService.Initialize(settings.Language);
        var moduleService = new ModuleService(settingsService, settings, _logger);
        await moduleService.LoadDefinitionsAsync();
        var settingCatalog = new SettingCatalogService(
            settingsService, settings, _logger, new SettingAvailabilityService());
        await settingCatalog.LoadAsync();
        var devicePageSettings = new DevicePageSettingsService(
            settingsService, settings, settingCatalog, _logger);

        var launcher = new WindowsSettingsLauncher(_logger);
        var main = new MainViewModel(
            moduleService,
            settingCatalog,
            devicePageSettings,
            new PowerSettingsService(_logger),
            new PowerPresetService(settingsService, settings),
            launcher,
            new ExplorerSettingsService(_logger),
            new WindowsUpdateStatusService(_logger),
            new SearchStatusService(_logger),
            new DeviceStatusService(_logger));

        var window = new MainWindow { DataContext = main };
        RestoreWindow(window, settings);
        MainWindow = window;
        window.Show();
        _singleInstance.ListenForActivation(() => Dispatcher.BeginInvoke(() => ActivateMainWindow(window)));
        window.Closing += async (_, args) =>
        {
            if (_allowWindowClose) return;
            args.Cancel = true;
            if (_isSavingOnClose) return;
            _isSavingOnClose = true;
            var result = await main.SaveWindowAsync(window);
            if (!result.IsSuccess)
                MessageBox.Show(result.UserMessage, "WinBridge", MessageBoxButton.OK, MessageBoxImage.Warning);
            _allowWindowClose = true;
            window.Close();
        };
        await main.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Info("アプリを終了しました。");
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void RestoreWindow(Window window, Models.AppSettings settings)
    {
        if (settings.WindowWidth >= 760) window.Width = settings.WindowWidth;
        if (settings.WindowHeight >= 520) window.Height = settings.WindowHeight;
        if (settings.WindowLeft is double left && settings.WindowTop is double top)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = left;
            window.Top = top;
        }
    }

    private static void ActivateMainWindow(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        if (!window.IsVisible)
            window.Show();
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}
