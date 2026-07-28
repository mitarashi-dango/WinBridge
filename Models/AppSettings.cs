namespace WinBridge.Models;

public sealed class AppSettings
{
    public int Version { get; set; } = 3;
    public List<ModulePreference> Modules { get; set; } = [];
    public List<SettingPreference> Settings { get; set; } = [];
    public List<string> Favorites { get; set; } = [];
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 720;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public string? LastModuleId { get; set; }
    public string? SelectedPowerPreset { get; set; }
}

public sealed class SettingPreference
{
    public required string Id { get; set; }
    public int Order { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsPinned { get; set; } = true;
}

public sealed class ModulePreference
{
    public required string Id { get; set; }
    public bool IsVisible { get; set; } = true;
    public int Order { get; set; }
}
