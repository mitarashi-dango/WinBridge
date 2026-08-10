using System.Collections.ObjectModel;
using System.Windows;
using WinBridge.Models;
using WinBridge.Services;

namespace WinBridge.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ModuleService _modules;
    private readonly SettingCatalogService _settingCatalog;
    private readonly WindowsSettingsLauncher _launcher;
    private object? _currentViewModel;
    private string _statusMessage = L.T("準備ができました。");
    private bool _hasError;
    private bool _isErrorDetailsVisible;
    private string _errorMessage = "";
    private string _errorTechnicalDetails = "";
    private string _errorOccurredAt = "";
    private readonly Dictionary<string, object> _pages = [];
    public ObservableCollection<ModuleDefinition> VisibleModules { get; } = [];
    public ObservableCollection<SettingDefinition> SelectedSettings => _settingCatalog.SelectedSettings;
    public ObservableCollection<SettingDefinition> PinnedSettings { get; } = [];
    public ObservableCollection<SettingCategoryViewModel> PinnedSettingGroups { get; } = [];
    public string VersionText { get; } = FormatVersion(typeof(MainViewModel).Assembly.GetName().Version);
    public object? CurrentViewModel { get => _currentViewModel; private set => SetProperty(ref _currentViewModel, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }
    public bool IsErrorDetailsVisible { get => _isErrorDetailsVisible; private set => SetProperty(ref _isErrorDetailsVisible, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string ErrorTechnicalDetails { get => _errorTechnicalDetails; private set => SetProperty(ref _errorTechnicalDetails, value); }
    public string ErrorOccurredAt { get => _errorOccurredAt; private set => SetProperty(ref _errorOccurredAt, value); }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand OpenSettingCommand { get; }
    public RelayCommand ToggleErrorDetailsCommand { get; }
    public RelayCommand DismissErrorCommand { get; }
    public HomeViewModel Home { get; }
    public PowerViewModel Power { get; }
    public WindowsUpdateViewModel WindowsUpdate { get; }
    public SearchViewModel Search { get; }
    public ExplorerViewModel Explorer { get; }
    public DeviceViewModel Devices { get; }
    public ModuleSettingsViewModel ModuleSettings { get; }
    public SettingsCatalogViewModel SettingsCatalog { get; }
    public AppPreferencesViewModel AppPreferences { get; }

    internal static string FormatVersion(Version? version) => version is null
        ? ""
        : $"v{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";

    public MainViewModel(ModuleService modules, SettingCatalogService settingCatalog,
        DevicePageSettingsService devicePageSettings,
        PowerSettingsService power, PowerPresetService powerPreset, WindowsSettingsLauncher launcher,
        ExplorerSettingsService explorer, WindowsUpdateStatusService updateStatus, SearchStatusService searchStatus,
        DeviceStatusService deviceStatus, ExternalLinkService externalLinks)
    {
        _modules = modules;
        _settingCatalog = settingCatalog;
        _launcher = launcher;
        NavigateCommand = new RelayCommand(p => Navigate(p?.ToString() ?? "home"));
        OpenSettingCommand = new RelayCommand(p => OpenSetting(p as SettingDefinition));
        ToggleErrorDetailsCommand = new RelayCommand(() => IsErrorDetailsVisible = !IsErrorDetailsVisible);
        DismissErrorCommand = new RelayCommand(ClearError);
        Home = new HomeViewModel(modules.Modules, settingCatalog.SelectedSettings, Navigate, OpenSetting);
        Power = new PowerViewModel(power, powerPreset, Report);
        WindowsUpdate = new WindowsUpdateViewModel(launcher, updateStatus, Report);
        Search = new SearchViewModel(launcher, searchStatus, Report);
        Explorer = new ExplorerViewModel(explorer, launcher, Report);
        Devices = new DeviceViewModel(deviceStatus, devicePageSettings, launcher, Report);
        ModuleSettings = new ModuleSettingsViewModel(modules, RefreshNavigation, Report);
        SettingsCatalog = new SettingsCatalogViewModel(settingCatalog, RefreshSettings, Report);
        AppPreferences = new AppPreferencesViewModel(modules, externalLinks, Report);
        _pages["home"] = Home;
        _pages["power"] = Power;
        _pages["windows-update"] = WindowsUpdate;
        _pages["search"] = Search;
        _pages["explorer"] = Explorer;
        _pages["devices"] = Devices;
        _pages["module-settings"] = ModuleSettings;
        _pages["settings-catalog"] = SettingsCatalog;
        _pages["app-preferences"] = AppPreferences;
        RefreshNavigation();
        RefreshSettings();
    }

    public async Task InitializeAsync()
    {
        // 起動時は前回表示していたページを復元せず、必ずホームから開始する。
        Navigate("home");
        await Power.RefreshAsync();
        WindowsUpdate.Refresh();
        await Search.RefreshAsync();
        // 非表示の機能は起動時にバックグラウンド処理も行わない。
        // 再表示後は、そのページを開いた時点で最新状態を取得する。
        if (_modules.IsVisible("devices"))
            await Devices.RefreshAsync();
    }

    public async Task<OperationResult> SaveWindowAsync(Window window)
    {
        if (window.WindowState == WindowState.Normal)
        {
            _modules.Settings.WindowWidth = window.Width;
            _modules.Settings.WindowHeight = window.Height;
            _modules.Settings.WindowLeft = window.Left;
            _modules.Settings.WindowTop = window.Top;
        }
        return await _modules.SaveAsync();
    }

    private void Navigate(string id)
    {
        if (!_pages.TryGetValue(id, out var page)) page = Home;
        CurrentViewModel = page;
        _modules.Settings.LastModuleId = id;
        if (page == Home) Home.Refresh();
        if (page == WindowsUpdate) WindowsUpdate.Refresh();
        if (page == Explorer) Explorer.Refresh();
        if (page == Devices)
        {
            Devices.RefreshSettingChoices();
            _ = Devices.RefreshAsync();
        }
    }

    private void RefreshNavigation()
    {
        VisibleModules.Clear();
        foreach (var module in _modules.Modules.Where(m => m.IsVisible).OrderBy(m => m.Order))
            VisibleModules.Add(module);
        Home.Refresh();
        if (CurrentViewModel is not null && CurrentViewModel != Home && CurrentViewModel != ModuleSettings)
        {
            var currentId = _pages.FirstOrDefault(p => ReferenceEquals(p.Value, CurrentViewModel)).Key;
            if (_modules.Modules.FirstOrDefault(m => m.Id == currentId)?.IsVisible == false) Navigate("home");
        }
    }

    private void RefreshSettings()
    {
        PinnedSettings.Clear();
        foreach (var setting in SelectedSettings.Where(s => s.IsPinned).OrderBy(s => s.Order))
            PinnedSettings.Add(setting);
        PinnedSettingGroups.Clear();
        foreach (var group in PinnedSettings
                     .GroupBy(setting => setting.Category)
                     .OrderBy(group => group.Key, StringComparer.CurrentCulture))
        {
            var category = new SettingCategoryViewModel { Name = group.Key };
            foreach (var setting in group.OrderBy(item => item.Order))
                category.Settings.Add(setting);
            PinnedSettingGroups.Add(category);
        }
        Home.Refresh();
        OnPropertyChanged(nameof(SelectedSettings));
    }

    private void OpenSetting(SettingDefinition? setting)
    {
        if (setting is null) return;
        Report(_launcher.Open(setting.Target));
    }

    private void Report(OperationResult result)
    {
        StatusMessage = result.UserMessage;
        if (result.IsSuccess)
        {
            ClearError();
            StatusMessage = result.UserMessage;
            return;
        }
        HasError = true;
        IsErrorDetailsVisible = false;
        ErrorMessage = result.UserMessage;
        ErrorTechnicalDetails = SanitizeTechnicalDetails(result.TechnicalDetails);
        ErrorOccurredAt = L.F("発生日時: {0:yyyy-MM-dd HH:mm:ss zzz}", DateTimeOffset.Now);
    }

    private void ClearError()
    {
        HasError = false;
        IsErrorDetailsVisible = false;
        ErrorMessage = "";
        ErrorTechnicalDetails = "";
        ErrorOccurredAt = "";
    }

    private static string SanitizeTechnicalDetails(string? details)
    {
        if (string.IsNullOrWhiteSpace(details)) return L.T("追加の技術情報はありません。");
        var result = details;
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
            result = result.Replace(profile, "[user-profile]", StringComparison.OrdinalIgnoreCase);
        return result.Replace(Environment.UserName, "[user]", StringComparison.OrdinalIgnoreCase);
    }
}
