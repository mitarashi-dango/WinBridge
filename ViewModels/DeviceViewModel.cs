using System.Collections.ObjectModel;
using WinBridge.Services;
using WinBridge.Models;

namespace WinBridge.ViewModels;

public sealed class DeviceViewModel : ObservableObject
{
    private readonly DeviceStatusService _deviceStatus;
    private readonly DevicePageSettingsService _pageSettings;
    private readonly WindowsSettingsLauncher _launcher;
    private readonly Action<OperationResult> _report;
    private string _summary = L.T("デバイスの状態を確認しています…");
    private string _searchText = "";

    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                RefreshSettingChoices();
        }
    }
    public ObservableCollection<SettingDefinition> SelectedSettings => _pageSettings.SelectedSettings;
    public ObservableCollection<SettingCategoryViewModel> AvailableSettingCategories { get; } = [];
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand OpenSettingCommand { get; }
    public RelayCommand AddSettingCommand { get; }
    public RelayCommand RemoveSettingCommand { get; }

    public DeviceViewModel(DeviceStatusService deviceStatus,
        DevicePageSettingsService pageSettings, WindowsSettingsLauncher launcher,
        Action<OperationResult> report)
    {
        _deviceStatus = deviceStatus;
        _pageSettings = pageSettings;
        _launcher = launcher;
        _report = report;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenCommand = new RelayCommand(Open);
        OpenSettingCommand = new RelayCommand(p => OpenSetting(p as SettingDefinition));
        AddSettingCommand = new RelayCommand(p => _ = AddSettingAsync(p as SettingDefinition));
        RemoveSettingCommand = new RelayCommand(p => _ = RemoveSettingAsync(p as SettingDefinition));
        RefreshSettingChoices();
    }

    public Task RefreshAsync()
    {
        var result = _deviceStatus.GetStatus();
        if (!result.IsSuccess || result.Value is null)
        {
            Summary = L.T(result.UserMessage);
            _report(OperationResult.Failure(result.UserMessage, result.TechnicalDetails));
            return Task.CompletedTask;
        }

        Summary = result.Value.ProblemDeviceCount == 0
            ? L.F("現在接続されているデバイスを確認しました（{0}件）。問題は報告されていません。",
                result.Value.PresentDeviceCount)
            : L.F("問題が報告されているデバイスが {0} 件あります。デバイスマネージャーで詳細を確認してください。",
                result.Value.ProblemDeviceCount);
        return Task.CompletedTask;
    }

    public void RefreshSettingChoices()
    {
        AvailableSettingCategories.Clear();
        var query = SearchText.Trim();
        var settings = _pageSettings.AvailableSettings
            .Where(setting => string.IsNullOrWhiteSpace(query) ||
                              setting.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                              setting.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                              setting.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                              setting.Keywords.Any(keyword =>
                                  keyword.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            .GroupBy(setting => setting.Category)
            .OrderBy(group => group.Key, StringComparer.CurrentCulture);

        foreach (var group in settings)
        {
            var category = new SettingCategoryViewModel { Name = group.Key };
            foreach (var setting in group.OrderBy(item => item.DisplayName, StringComparer.CurrentCulture))
                category.Settings.Add(setting);
            AvailableSettingCategories.Add(category);
        }
        OnPropertyChanged(nameof(SelectedSettings));
    }

    private void Open(object? parameter)
    {
        var target = parameter?.ToString() ?? "";
        var result = target == "device-manager"
            ? _launcher.OpenDeviceManager()
            : _launcher.Open(target);
        _report(result);
    }

    private void OpenSetting(SettingDefinition? setting)
    {
        if (setting is not null)
            _report(_launcher.Open(setting.Target));
    }

    private async Task AddSettingAsync(SettingDefinition? setting)
    {
        if (setting is null) return;
        var result = await _pageSettings.AddAsync(setting);
        RefreshSettingChoices();
        _report(result.IsSuccess
            ? OperationResult.Success(L.F("「{0}」をデバイスページへ追加しました。", setting.DisplayName))
            : result);
    }

    private async Task RemoveSettingAsync(SettingDefinition? setting)
    {
        if (setting is null) return;
        var result = await _pageSettings.RemoveAsync(setting);
        RefreshSettingChoices();
        _report(result.IsSuccess
            ? OperationResult.Success(
                L.F("「{0}」をデバイスページから外しました。Windowsの設定は変更されていません。",
                    setting.DisplayName))
            : result);
    }
}
