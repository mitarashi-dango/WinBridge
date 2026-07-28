using System.Collections.ObjectModel;
using WinBridge.Models;
using WinBridge.Services;

namespace WinBridge.ViewModels;

public sealed class AppPreferencesViewModel : ObservableObject
{
    private readonly ModuleService _modules;
    private readonly Action<OperationResult> _report;
    private LanguageOption? _selectedLanguage;

    public ObservableCollection<LanguageOption> Languages { get; } =
    [
        new("system", L.T("Windowsの表示言語に合わせる")),
        new("ja-JP", "日本語"),
        new("en-US", "English")
    ];

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public AsyncRelayCommand SaveCommand { get; }

    public AppPreferencesViewModel(ModuleService modules, Action<OperationResult> report)
    {
        _modules = modules;
        _report = report;
        SelectedLanguage = Languages.FirstOrDefault(option =>
                               string.Equals(option.Value, modules.Settings.Language,
                                   StringComparison.OrdinalIgnoreCase))
                           ?? Languages[0];
        SaveCommand = new AsyncRelayCommand(SaveAsync);
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
