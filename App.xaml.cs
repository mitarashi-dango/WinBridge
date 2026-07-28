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
        var visibleArea = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        if (double.IsFinite(settings.WindowWidth) && settings.WindowWidth >= 760)
            window.Width = Math.Min(settings.WindowWidth, visibleArea.Width);
        if (double.IsFinite(settings.WindowHeight) && settings.WindowHeight >= 520)
            window.Height = Math.Min(settings.WindowHeight, visibleArea.Height);
        if (settings.WindowLeft is double left && settings.WindowTop is double top &&
            double.IsFinite(left) && double.IsFinite(top))
        {
            var restored = ClampToVisibleArea(
                new Rect(left, top, window.Width, window.Height), visibleArea);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = restored.Left;
            window.Top = restored.Top;
        }
    }

    internal static Rect ClampToVisibleArea(Rect desired, Rect visibleArea)
    {
        if (visibleArea.IsEmpty || visibleArea.Width <= 0 || visibleArea.Height <= 0)
            return desired;

        var width = Math.Min(desired.Width, visibleArea.Width);
        var height = Math.Min(desired.Height, visibleArea.Height);
        var maxLeft = Math.Max(visibleArea.Left, visibleArea.Right - width);
        var maxTop = Math.Max(visibleArea.Top, visibleArea.Bottom - height);
        var left = Math.Clamp(desired.Left, visibleArea.Left, maxLeft);
        var top = Math.Clamp(desired.Top, visibleArea.Top, maxTop);
        return new Rect(left, top, width, height);
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
