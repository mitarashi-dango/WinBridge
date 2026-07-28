using WinBridge.ViewModels;

namespace WinBridge.Models;

public sealed class SettingDefinition : ObservableObject
{
    private bool _isSelected;
    private bool _isFavorite;
    private bool _isPinned = true;
    private int _order;

    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public string Icon { get; init; } = "⚙";
    public required string Target { get; init; }
    public List<string> Keywords { get; init; } = [];
    public SafetyClass SafetyClass { get; init; } = SafetyClass.OpenWindowsSettings;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool IsFavorite { get => _isFavorite; set => SetProperty(ref _isFavorite, value); }
    public bool IsPinned { get => _isPinned; set => SetProperty(ref _isPinned, value); }
    public int Order { get => _order; set => SetProperty(ref _order, value); }
}
