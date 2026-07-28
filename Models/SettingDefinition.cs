using WinBridge.ViewModels;

namespace WinBridge.Models;

public enum SettingAvailability
{
    Always,
    Battery,
    Touch,
    Touchpad,
    Pen,
    SurfaceDial,
    EyeTracker,
    HearingDevice,
    Cellular,
    DirectAccess,
    AdvancedDisplay,
    Graphics,
    PresenceSensing,
    WindowsInsider,
    WindowsHelloFace,
    WindowsHelloFingerprint,
    SecurityKey,
    DynamicLighting,
    CopilotKey
}

public sealed class SettingDefinition : ObservableObject
{
    private bool _isSelected;
    private bool _isFavorite;
    private bool _isPinned = true;
    private bool _isAvailable = true;
    private int _order;

    public required string Id { get; init; }
    public required string DisplayName { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public string Icon { get; init; } = "⚙";
    public required string Target { get; init; }
    public List<string> Keywords { get; set; } = [];
    public SafetyClass SafetyClass { get; init; } = SafetyClass.OpenWindowsSettings;
    public SettingAvailability Availability { get; init; } = SettingAvailability.Always;
    public bool IsAvailable { get => _isAvailable; internal set => SetProperty(ref _isAvailable, value); }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool IsFavorite { get => _isFavorite; set => SetProperty(ref _isFavorite, value); }
    public bool IsPinned { get => _isPinned; set => SetProperty(ref _isPinned, value); }
    public int Order { get => _order; set => SetProperty(ref _order, value); }
}
