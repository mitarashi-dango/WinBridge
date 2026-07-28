using System.Collections.ObjectModel;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed class DevicePageSettingsService
{
    private const string DeviceIdPrefix = "devices.";
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly SettingCatalogService _catalog;
    private readonly LoggingService _logger;

    public ObservableCollection<SettingDefinition> SelectedSettings { get; } = [];
    public IEnumerable<SettingDefinition> AvailableSettings =>
        _catalog.AllSettings.Where(setting =>
            setting.IsAvailable &&
            setting.Id.StartsWith(DeviceIdPrefix, StringComparison.OrdinalIgnoreCase) &&
            !_settings.DevicePageSettings.Contains(setting.Id, StringComparer.OrdinalIgnoreCase));

    public DevicePageSettingsService(AppSettingsService settingsService, AppSettings settings,
        SettingCatalogService catalog, LoggingService logger)
    {
        _settingsService = settingsService;
        _settings = settings;
        _catalog = catalog;
        _logger = logger;
        Refresh();
    }

    public async Task<OperationResult> AddAsync(SettingDefinition setting)
    {
        if (!setting.Id.StartsWith(DeviceIdPrefix, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Failure("デバイスページにはデバイスカテゴリの設定だけ追加できます。");
        if (!setting.IsAvailable)
            return OperationResult.Failure("この設定は現在の端末では利用できません。");
        if (_settings.DevicePageSettings.Contains(setting.Id, StringComparer.OrdinalIgnoreCase))
            return OperationResult.Success("この設定はデバイスページに追加済みです。");

        _settings.DevicePageSettings.Add(setting.Id);
        Refresh();
        _logger.Info($"デバイスページへWindows設定を追加しました: {setting.Id}");
        return await SaveAsync();
    }

    public async Task<OperationResult> RemoveAsync(SettingDefinition setting)
    {
        _settings.DevicePageSettings.RemoveAll(id =>
            string.Equals(id, setting.Id, StringComparison.OrdinalIgnoreCase));
        Refresh();
        _logger.Info($"デバイスページからWindows設定を外しました: {setting.Id}");
        return await SaveAsync();
    }

    public void Refresh()
    {
        SelectedSettings.Clear();
        foreach (var id in _settings.DevicePageSettings)
        {
            var setting = _catalog.AllSettings.FirstOrDefault(candidate =>
                candidate.IsAvailable &&
                string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            if (setting is not null)
                SelectedSettings.Add(setting);
        }
    }

    private Task<OperationResult> SaveAsync()
    {
        _settings.Version = AppSettingsMigrator.CurrentVersion;
        return _settingsService.SaveAsync(_settings);
    }
}
