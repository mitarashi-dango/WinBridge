using System.Collections.ObjectModel;
using WinBridge.Models;

namespace WinBridge.ViewModels;

public sealed class SettingCategoryViewModel
{
    public required string Name { get; init; }
    public ObservableCollection<SettingDefinition> Settings { get; } = [];
}
