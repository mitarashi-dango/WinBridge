using System.Collections.ObjectModel;
using WinBridge.Models;
using WinBridge.Services;

namespace WinBridge.ViewModels;

public sealed class AppPreferencesViewModel : ObservableObject
{
    private readonly ModuleService _modules;
    private readonly ExternalLinkService _externalLinks;
    private readonly Action<OperationResult> _report;
    private LanguageOption? _selectedLanguage;

    public ObservableCollection<LanguageOption> Languages { get; } =
    [
        new("system", L.T("Windowsの表示言語に合わせる")),
        new("ja-JP", "日本語"),
        new("en-US", "English"),
        new("zh-TW", "中文（繁體）"),
        new("zh-CN", "中文（简体）"),
        new("es-ES", "Español")
    ];

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand OpenSupportPageCommand { get; }

    public AppPreferencesViewModel(ModuleService modules, ExternalLinkService externalLinks,
        Action<OperationResult> report)
    {
        _modules = modules;
        _externalLinks = externalLinks;
        _report = report;
        SelectedLanguage = Languages.FirstOrDefault(option =>
                               string.Equals(option.Value, modules.Settings.Language,
                                   StringComparison.OrdinalIgnoreCase))
                           ?? Languages[0];
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        OpenSupportPageCommand = new RelayCommand(() => _report(_externalLinks.OpenSupportPage()));
    }

    private async Task SaveAsync()
    {
        if (SelectedLanguage is null) return;
        _modules.Settings.Language = SelectedLanguage.Value;
        var result = await _modules.SaveAsync();
        _report(result.IsSuccess
            ? OperationResult.Success("表示言語を保存しました。WinBridgeを再起動すると反映されます。")
            : result);
    }
}

public sealed record LanguageOption(string Value, string DisplayName);
