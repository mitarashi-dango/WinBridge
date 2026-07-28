using System.Collections.ObjectModel;
using WinBridge.Models;
using WinBridge.Services;

namespace WinBridge.ViewModels;

public sealed class ModuleSettingsViewModel
{
    private readonly ModuleService _service;
    private readonly Action _changed;
    private readonly Action<OperationResult> _report;
    public ObservableCollection<ModuleDefinition> Modules => _service.Modules;
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ToggleFavoriteCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }

    public ModuleSettingsViewModel(ModuleService service, Action changed, Action<OperationResult> report)
    {
        _service = service; _changed = changed; _report = report;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ToggleFavoriteCommand = new AsyncRelayCommand(SaveAsync);
        MoveUpCommand = new RelayCommand(p => _ = MoveAsync(p as ModuleDefinition, -1));
        MoveDownCommand = new RelayCommand(p => _ = MoveAsync(p as ModuleDefinition, 1));
    }

    public async Task MoveToAsync(ModuleDefinition module, int index)
    {
        var result = await _service.MoveAsync(module, index);
        _changed();
        _report(result.IsSuccess ? OperationResult.Success("表示順を変更しました。") : result);
    }

    private async Task MoveAsync(ModuleDefinition? module, int delta)
    {
        if (module is null) return;
        await MoveToAsync(module, _service.Modules.IndexOf(module) + delta);
    }

    private async Task SaveAsync()
    {
        var result = await _service.SaveAsync();
        _changed();
        _report(result.IsSuccess ? OperationResult.Success("表示する機能を保存しました。") : result);
    }
}
