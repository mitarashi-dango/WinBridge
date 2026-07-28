using WinBridge.Models;

namespace WinBridge.Services;

public sealed record SettingsMigrationResult(AppSettings Settings, bool CanSave, string? Warning = null);

public static class AppSettingsMigrator
{
    public const int CurrentVersion = 3;
    private static readonly string[] InitialSettings =
        ["system.display", "system.sound", "devices.bluetooth"];

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

        Normalize(settings);
        return new SettingsMigrationResult(settings, true);
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Modules ??= [];
        settings.Settings ??= [];
        settings.Favorites ??= [];
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
    }
}
