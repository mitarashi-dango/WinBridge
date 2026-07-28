using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using WinBridge.Models;
using WinBridge.Services;

namespace WinBridge.ViewModels;

public sealed class SettingsCatalogViewModel : ObservableObject
{
    private readonly SettingCatalogService _catalog;
    private readonly Action _changed;
    private readonly Action<OperationResult> _report;
    private string _searchText = "";

    public ObservableCollection<SettingDefinition> SelectedSettings => _catalog.SelectedSettings;
    public ICollectionView AvailableSettingsView { get; }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                AvailableSettingsView.Refresh();
        }
    }

    public RelayCommand AddCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }

    public SettingsCatalogViewModel(SettingCatalogService catalog, Action changed, Action<OperationResult> report)
    {
        _catalog = catalog;
        _changed = changed;
        _report = report;
        AvailableSettingsView = CollectionViewSource.GetDefaultView(_catalog.AllSettings);
        AvailableSettingsView.Filter = IsAvailable;
        AvailableSettingsView.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(SettingDefinition.Category)));
        AvailableSettingsView.SortDescriptions.Add(
            new SortDescription(nameof(SettingDefinition.DisplayName), ListSortDirection.Ascending));

        AddCommand = new RelayCommand(p => _ = AddAsync(p as SettingDefinition));
        RemoveCommand = new RelayCommand(p => _ = RemoveAsync(p as SettingDefinition));
        MoveUpCommand = new RelayCommand(p => _ = MoveAsync(p as SettingDefinition, -1));
        MoveDownCommand = new RelayCommand(p => _ = MoveAsync(p as SettingDefinition, 1));
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public async Task MoveToAsync(SettingDefinition setting, int index)
    {
        var result = await _catalog.MoveAsync(setting, index);
        Changed(result, "設定の表示順を変更しました。");
    }

    private bool IsAvailable(object item)
    {
        if (item is not SettingDefinition setting || setting.IsSelected) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var query = SearchText.Trim();
        return setting.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || setting.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || setting.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || setting.Keywords.Any(k => k.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private async Task AddAsync(SettingDefinition? setting)
    {
        if (setting is null) return;
        var result = await _catalog.AddAsync(setting);
        AvailableSettingsView.Refresh();
        Changed(result, $"「{setting.DisplayName}」を追加しました。");
    }

    private async Task RemoveAsync(SettingDefinition? setting)
    {
        if (setting is null) return;
        var result = await _catalog.RemoveAsync(setting);
        AvailableSettingsView.Refresh();
        Changed(result, $"「{setting.DisplayName}」をWinBridgeから外しました。Windowsの設定は変更されていません。");
    }

    private async Task MoveAsync(SettingDefinition? setting, int delta)
    {
        if (setting is null) return;
        await MoveToAsync(setting, SelectedSettings.IndexOf(setting) + delta);
    }

    private async Task SaveAsync()
    {
        var result = await _catalog.SaveAsync();
        Changed(result, "設定の表示方法を保存しました。");
    }

    private void Changed(OperationResult result, string successMessage)
    {
        _changed();
        _report(result.IsSuccess ? OperationResult.Success(successMessage) : result);
    }
}
