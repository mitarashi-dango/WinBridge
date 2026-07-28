using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed class ModuleService
{
    private readonly AppSettingsService _settingsService;
    private readonly LoggingService _logger;
    public AppSettings Settings { get; }
    public ObservableCollection<ModuleDefinition> Modules { get; } = [];

    public ModuleService(AppSettingsService settingsService, AppSettings settings, LoggingService logger)
    {
        _settingsService = settingsService;
        Settings = settings;
        _logger = logger;
    }

    public async Task LoadDefinitionsAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "ModuleDefinitions.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        await using var stream = File.OpenRead(path);
        var definitions = await JsonSerializer.DeserializeAsync<List<ModuleDefinition>>(stream, options) ?? [];
        var validIds = definitions.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Settings.Modules.RemoveAll(p => !validIds.Contains(p.Id));

        foreach (var definition in definitions)
        {
            CatalogLocalizationService.Localize(definition);
            var preference = Settings.Modules.FirstOrDefault(p =>
                string.Equals(p.Id, definition.Id, StringComparison.OrdinalIgnoreCase));
            if (preference is not null)
            {
                definition.IsVisible = preference.IsVisible;
                definition.Order = preference.Order;
            }
            definition.IsFavorite = Settings.Favorites.Contains(definition.Id, StringComparer.OrdinalIgnoreCase);
            Modules.Add(definition);
        }
        Sort();
    }

    public async Task<OperationResult> SaveAsync()
    {
        for (var i = 0; i < Modules.Count; i++) Modules[i].Order = i + 1;
        Settings.Modules = Modules.Select(m => new ModulePreference
            { Id = m.Id, IsVisible = m.IsVisible, Order = m.Order }).ToList();
        Settings.Favorites = Modules.Where(m => m.IsFavorite).Select(m => m.Id).ToList();
        return await _settingsService.SaveAsync(Settings);
    }

    public async Task<OperationResult> MoveAsync(ModuleDefinition item, int targetIndex)
    {
        var oldIndex = Modules.IndexOf(item);
        targetIndex = Math.Clamp(targetIndex, 0, Modules.Count - 1);
        if (oldIndex < 0 || oldIndex == targetIndex)
            return OperationResult.Success("表示順は変更されていません。");
        Modules.Move(oldIndex, targetIndex);
        var result = await SaveAsync();
        _logger.Info($"モジュールの順序を変更しました: {item.Id}");
        return result;
    }

    public bool IsVisible(string id) =>
        Modules.FirstOrDefault(module =>
            string.Equals(module.Id, id, StringComparison.OrdinalIgnoreCase))?.IsVisible == true;

    public void Sort()
    {
        var ordered = Modules.OrderBy(m => m.Order).ToList();
        Modules.Clear();
        foreach (var module in ordered) Modules.Add(module);
    }
}
