using WinBridge.Models;

namespace WinBridge.Services;

public sealed record SettingsMigrationResult(AppSettings Settings, bool CanSave, string? Warning = null);

public static class AppSettingsMigrator
{
    public const int CurrentVersion = 7;
    private static readonly string[] InitialSettings =
        ["system.display", "system.sound", "devices.bluetooth"];
    private static readonly string[] InitialDevicePageSettings =
    [
        "devices.mouse",
        "devices.typing",
        "devices.microphone",
        "devices.printers",
        "devices.camera",
        "devices.bluetooth"
    ];
    private static readonly string[] PreviousInitialDevicePageSettings =
    [
        "devices.bluetooth",
        "devices.printers",
        "devices.usb",
        "devices.camera",
        "devices.mouse",
        "devices.microphone"
    ];

    public static SettingsMigrationResult Migrate(AppSettings settings)
    {
        Normalize(settings);

        if (settings.Version > CurrentVersion)
        {
            return new SettingsMigrationResult(settings, false,
                "この設定ファイルは新しいWinBridgeで作成されています。上書きを防ぐため読み取り専用で開きます。");
        }

        if (settings.Version <= 0)
            settings.Version = 1;

        if (settings.Version == 1)
        {
            if (settings.Settings.Count == 0)
            {
                settings.Settings = InitialSettings.Select((id, index) => new SettingPreference
                {
                    Id = id,
                    Order = index + 1,
                    IsPinned = true
                }).ToList();
            }
            settings.Version = 2;
        }

        if (settings.Version == 2)
        {
            foreach (var setting in settings.Settings)
                setting.IsPinned = true;
            settings.Version = 3;
        }

        if (settings.Version == 3)
        {
            settings.DevicePageSettings = [.. InitialDevicePageSettings];
            settings.Version = 4;
        }

        if (settings.Version == 4)
        {
            if (settings.DevicePageSettings.SequenceEqual(
                    PreviousInitialDevicePageSettings, StringComparer.OrdinalIgnoreCase))
                settings.DevicePageSettings = [.. InitialDevicePageSettings];
            settings.Version = 5;
        }

        if (settings.Version == 5)
        {
            settings.Language = NormalizeLanguage(settings.Language);
            settings.Version = 6;
        }

        if (settings.Version == 6)
        {
            settings.CustomPowerPreset ??= new PowerPresetSettings();
            settings.Version = 7;
        }

        Normalize(settings);
        return new SettingsMigrationResult(settings, true);
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Modules ??= [];
        settings.Settings ??= [];
        settings.DevicePageSettings ??= [];
        settings.Favorites ??= [];
        settings.CustomPowerPreset ??= new PowerPresetSettings();
        settings.Modules = settings.Modules
            .Where(m => !string.IsNullOrWhiteSpace(m.Id))
            .GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        settings.Settings = settings.Settings
            .Where(s => !string.IsNullOrWhiteSpace(s.Id))
            .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        settings.Favorites = settings.Favorites
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.DevicePageSettings = settings.DevicePageSettings
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.Language = NormalizeLanguage(settings.Language);
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.Equals(language, "ja-JP", StringComparison.OrdinalIgnoreCase)) return "ja-JP";
        if (string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase)) return "en-US";
        return "system";
    }
}
