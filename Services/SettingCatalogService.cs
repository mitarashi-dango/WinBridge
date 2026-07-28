using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed class SettingCatalogService
{
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly LoggingService _logger;
    private readonly ISettingAvailabilityService _availabilityService;
    private readonly List<SettingPreference> _unavailablePreferences = [];

    public ObservableCollection<SettingDefinition> AllSettings { get; } = [];
    public ObservableCollection<SettingDefinition> SelectedSettings { get; } = [];

    public SettingCatalogService(AppSettingsService settingsService, AppSettings settings,
        LoggingService logger, ISettingAvailabilityService availabilityService)
    {
        _settingsService = settingsService;
        _settings = settings;
        _logger = logger;
        _availabilityService = availabilityService;
    }

    public async Task LoadAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "SettingDefinitions.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        await using var stream = File.OpenRead(path);
        var definitions = await JsonSerializer.DeserializeAsync<List<SettingDefinition>>(stream, options) ?? [];

        foreach (var definition in definitions.Where(IsSafeDefinition))
        {
            CatalogLocalizationService.Localize(definition);
            definition.IsAvailable = _availabilityService.IsAvailable(definition.Availability);
            AllSettings.Add(definition);
        }

        var preferences = _settings.Settings;

        foreach (var preference in preferences.OrderBy(p => p.Order))
        {
            var definition = AllSettings.FirstOrDefault(s =>
                string.Equals(s.Id, preference.Id, StringComparison.OrdinalIgnoreCase));
            if (definition is null) continue;
            if (!definition.IsAvailable)
            {
                _unavailablePreferences.Add(Clone(preference));
                continue;
            }
            definition.IsSelected = true;
            definition.IsFavorite = preference.IsFavorite;
            definition.IsPinned = preference.IsPinned;
            definition.Order = preference.Order;
            SelectedSettings.Add(definition);
        }

        var saveResult = await SaveAsync();
        if (!saveResult.IsSuccess)
            _logger.Error(saveResult.UserMessage);
    }

    public async Task<OperationResult> AddAsync(SettingDefinition setting)
    {
        if (!setting.IsAvailable)
            return OperationResult.Failure("この設定は現在の端末では利用できません。");
        if (setting.IsSelected) return OperationResult.Success("この設定は追加済みです。");
        setting.IsSelected = true;
        setting.IsPinned = true;
        setting.Order = SelectedSettings.Count + 1;
        SelectedSettings.Add(setting);
        _logger.Info($"Windows設定を追加しました: {setting.Id}");
        return await SaveAsync();
    }

    public async Task<OperationResult> RemoveAsync(SettingDefinition setting)
    {
        if (!setting.IsSelected) return OperationResult.Success("この設定は追加されていません。");
        SelectedSettings.Remove(setting);
        setting.IsSelected = false;
        setting.IsFavorite = false;
        setting.IsPinned = true;
        setting.Order = 0;
        _logger.Info($"Windows設定を外しました: {setting.Id}");
        return await SaveAsync();
    }

    public async Task<OperationResult> MoveAsync(SettingDefinition setting, int targetIndex)
    {
        var oldIndex = SelectedSettings.IndexOf(setting);
        targetIndex = Math.Clamp(targetIndex, 0, SelectedSettings.Count - 1);
        if (oldIndex < 0 || oldIndex == targetIndex)
            return OperationResult.Success("表示順は変更されていません。");
        SelectedSettings.Move(oldIndex, targetIndex);
        return await SaveAsync();
    }

    public async Task<OperationResult> SaveAsync()
    {
        for (var i = 0; i < SelectedSettings.Count; i++)
            SelectedSettings[i].Order = i + 1;
        var visiblePreferences = SelectedSettings.Select(s => new SettingPreference
        {
            Id = s.Id,
            Order = s.Order,
            IsFavorite = s.IsFavorite,
            IsPinned = s.IsPinned
        });
        _settings.Settings = visiblePreferences
            .Concat(_unavailablePreferences.Select(Clone))
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        _settings.Version = AppSettingsMigrator.CurrentVersion;
        return await _settingsService.SaveAsync(_settings);
    }

    private bool IsSafeDefinition(SettingDefinition setting)
    {
        if (Uri.TryCreate(setting.Target, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, "ms-settings", StringComparison.OrdinalIgnoreCase))
            return true;
        _logger.Error($"安全でない設定カタログ項目を無視しました: {setting.Id}");
        return false;
    }

    private static SettingPreference Clone(SettingPreference preference) => new()
    {
        Id = preference.Id,
        Order = preference.Order,
        IsFavorite = preference.IsFavorite,
        IsPinned = preference.IsPinned
    };
}
