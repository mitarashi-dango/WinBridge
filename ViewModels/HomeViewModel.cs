using System.Collections.ObjectModel;
using WinBridge.Models;

namespace WinBridge.ViewModels;

public sealed class HomeCardViewModel
{
    public required ModuleDefinition Module { get; init; }
    public required RelayCommand OpenCommand { get; init; }
}

public sealed class SettingShortcutViewModel
{
    public required SettingDefinition Setting { get; init; }
    public required RelayCommand OpenCommand { get; init; }
}

public sealed class HomeViewModel : ObservableObject
{
    public ObservableCollection<HomeCardViewModel> Favorites { get; } = [];
    public ObservableCollection<HomeCardViewModel> Cards { get; } = [];
    public ObservableCollection<SettingShortcutViewModel> Settings { get; } = [];
    public RelayCommand OpenModuleSettingsCommand { get; }
    public RelayCommand OpenSettingsCatalogCommand { get; }
    private readonly Action<string> _navigate;
    private readonly IEnumerable<ModuleDefinition> _modules;
    private readonly IEnumerable<SettingDefinition> _settings;
    private readonly Action<SettingDefinition> _openSetting;

    public HomeViewModel(IEnumerable<ModuleDefinition> modules, IEnumerable<SettingDefinition> settings,
        Action<string> navigate, Action<SettingDefinition> openSetting)
    {
        _modules = modules;
        _settings = settings;
        _navigate = navigate;
        _openSetting = openSetting;
        OpenModuleSettingsCommand = new RelayCommand(() => navigate("module-settings"));
        OpenSettingsCatalogCommand = new RelayCommand(() => navigate("settings-catalog"));
        Refresh();
    }

    public void Refresh()
    {
        Favorites.Clear();
        Cards.Clear();
        Settings.Clear();
        foreach (var setting in _settings.OrderByDescending(s => s.IsFavorite).ThenBy(s => s.Order))
        {
            Settings.Add(new SettingShortcutViewModel
            {
                Setting = setting,
                OpenCommand = new RelayCommand(() => _openSetting(setting))
            });
        }
        foreach (var module in _modules.Where(m => m.IsVisible).OrderBy(m => m.Order))
        {
            var card = new HomeCardViewModel
            {
                Module = module,
                OpenCommand = new RelayCommand(() => _navigate(module.Id))
            };
            Cards.Add(card);
            if (module.IsFavorite) Favorites.Add(card);
        }
    }
}
