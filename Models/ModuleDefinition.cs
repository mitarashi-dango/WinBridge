using WinBridge.ViewModels;

namespace WinBridge.Models;

public enum SafetyClass { DirectChange, OpenWindowsSettings, GuidanceOnly }

public sealed class ModuleDefinition : ObservableObject
{
    private bool _isVisible = true;
    private int _order;
    private bool _isFavorite;

    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public string Icon { get; init; } = "⚙";
    public bool IsAvailable { get; init; } = true;
    public bool RequiresAdministrator { get; init; }
    public string? SettingsUri { get; init; }
    public SafetyClass SafetyClass { get; init; }
    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    public int Order { get => _order; set => SetProperty(ref _order, value); }
    public bool IsFavorite { get => _isFavorite; set => SetProperty(ref _isFavorite, value); }
}
