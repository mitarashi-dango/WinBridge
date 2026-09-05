using System.Globalization;
using WinBridge.Models;

namespace WinBridge.Services;

public static class CatalogLocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> ModuleNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["power"] = "Screen and sleep",
            ["windows-update"] = "Windows Update",
            ["search"] = "Start and search",
            ["explorer"] = "File Explorer",
            ["devices"] = "Devices and connections"
        };

    private static readonly IReadOnlyDictionary<string, string> ModuleDescriptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["power"] = "View and change the screen-off and sleep timers.",
            ["windows-update"] = "Open official Windows pages for updates, history, and restart settings.",
            ["search"] = "Review search settings and guidance for common problems.",
            ["explorer"] = "Manage file display options and safely restart File Explorer.",
            ["devices"] = "Review device status and collect the device settings you use."
        };

    private static readonly IReadOnlyDictionary<string, string> Categories =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["system"] = "System",
            ["devices"] = "Devices",
            ["network"] = "Network & internet",
            ["personalization"] = "Personalization",
            ["apps"] = "Apps",
            ["accounts"] = "Accounts",
            ["time"] = "Time & language",
            ["gaming"] = "Gaming",
            ["accessibility"] = "Accessibility",
            ["privacy"] = "Privacy & security",
            ["update"] = "Windows Update",
            ["family"] = "Accounts"
        };

    private static readonly IReadOnlyDictionary<string, string> SettingNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["devices.mouse"] = "Mouse",
            ["devices.typing"] = "Typing",
            ["devices.microphone"] = "Microphone input",
            ["devices.printers"] = "Printers & scanners",
            ["devices.camera"] = "Camera",
            ["devices.bluetooth"] = "Bluetooth & devices",
            ["devices.usb"] = "USB",
            ["devices.autoplay"] = "AutoPlay",
            ["devices.pen"] = "Pen & Windows Ink",
            ["devices.touch"] = "Touch",
            ["devices.touchpad"] = "Touchpad",
            ["devices.text-suggestions"] = "Hardware keyboard text suggestions",
            ["devices.surface-dial"] = "Surface Dial",
            ["devices.hearing-devices"] = "Hearing devices",
            ["system.display"] = "Display",
            ["system.sound"] = "Sound",
            ["system.notifications"] = "Notifications",
            ["system.focus"] = "Focus",
            ["system.power"] = "Power & battery",
            ["system.storage"] = "Storage",
            ["network.wifi"] = "Wi-Fi",
            ["network.ethernet"] = "Ethernet",
            ["network.vpn"] = "VPN",
            ["network.proxy"] = "Proxy",
            ["personalization.background"] = "Background",
            ["personalization.colors"] = "Colors",
            ["personalization.themes"] = "Themes",
            ["personalization.lock-screen"] = "Lock screen",
            ["personalization.taskbar"] = "Taskbar",
            ["personalization.start"] = "Start",
            ["apps.installed"] = "Installed apps",
            ["apps.default"] = "Default apps",
            ["apps.startup"] = "Startup apps",
            ["accounts.info"] = "Your info",
            ["accounts.email"] = "Email & accounts",
            ["accounts.signin"] = "Sign-in options",
            ["time.date"] = "Date & time",
            ["time.language"] = "Language & region",
            ["time.typing"] = "Typing",
            ["accessibility.vision"] = "Vision",
            ["accessibility.hearing"] = "Hearing",
            ["accessibility.interaction"] = "Interaction",
            ["privacy.location"] = "Location",
            ["privacy.camera"] = "Camera permissions",
            ["privacy.microphone"] = "Microphone permissions",
            ["update.main"] = "Windows Update",
            ["update.history"] = "Update history",
            ["update.optional"] = "Optional updates"
        };

    private static readonly IReadOnlyDictionary<string, string> Words =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["main"] = "Overview", ["advanced"] = "Advanced settings", ["options"] = "Options",
            ["history"] = "History", ["optional"] = "Optional updates", ["restart"] = "Restart",
            ["activehours"] = "Active hours", ["restart-options"] = "Restart options",
            ["battery-saver"] = "Energy saver", ["remote-desktop"] = "Remote Desktop",
            ["nearby-sharing"] = "Nearby sharing", ["multitasking"] = "Multitasking",
            ["clipboard"] = "Clipboard", ["recovery"] = "Recovery", ["about"] = "About",
            ["graphics"] = "Graphics", ["display"] = "Display", ["sound"] = "Sound",
            ["notifications"] = "Notifications", ["storage"] = "Storage", ["power"] = "Power",
            ["bluetooth"] = "Bluetooth", ["camera"] = "Camera", ["mouse"] = "Mouse",
            ["typing"] = "Typing", ["printers"] = "Printers & scanners", ["microphone"] = "Microphone",
            ["wifi"] = "Wi-Fi", ["ethernet"] = "Ethernet", ["vpn"] = "VPN", ["proxy"] = "Proxy",
            ["airplane"] = "Airplane mode", ["cellular"] = "Cellular", ["hotspot"] = "Mobile hotspot",
            ["dialup"] = "Dial-up", ["status"] = "Status", ["data-usage"] = "Data usage",
            ["background"] = "Background", ["colors"] = "Colors", ["themes"] = "Themes",
            ["fonts"] = "Fonts", ["taskbar"] = "Taskbar", ["start"] = "Start",
            ["installed"] = "Installed apps", ["default"] = "Default apps", ["startup"] = "Startup apps",
            ["maps"] = "Offline maps", ["video-playback"] = "Video playback",
            ["info"] = "Your info", ["email"] = "Email & accounts", ["signin"] = "Sign-in options",
            ["family"] = "Family", ["backup"] = "Windows backup", ["workplace"] = "Access work or school",
            ["date"] = "Date & time", ["language"] = "Language & region", ["speech"] = "Speech",
            ["location"] = "Location", ["general"] = "General", ["diagnostics"] = "Diagnostics & feedback",
            ["camera"] = "Camera", ["microphone"] = "Microphone", ["documents"] = "Documents",
            ["pictures"] = "Pictures", ["videos"] = "Videos", ["filesystem"] = "File system",
            ["game-bar"] = "Game Bar", ["captures"] = "Captures", ["mode"] = "Game Mode",
            ["audio"] = "Audio", ["captions"] = "Captions", ["magnifier"] = "Magnifier",
            ["narrator"] = "Narrator", ["keyboard"] = "Keyboard", ["pointer"] = "Mouse pointer and touch",
            ["eye-control"] = "Eye control", ["visual-effects"] = "Visual effects",
            ["contrast"] = "Contrast themes", ["text-cursor"] = "Text cursor"
        };

    public static void Localize(ModuleDefinition definition)
    {
        var language = LocalizationService.CurrentLanguage;
        if (language == "ja-JP") return;
        if (language == "en-US")
        {
            if (ModuleNames.TryGetValue(definition.Id, out var name)) definition.DisplayName = name;
            if (ModuleDescriptions.TryGetValue(definition.Id, out var description))
                definition.Description = description;
            return;
        }

        definition.DisplayName = L.T(definition.DisplayName);
        var localizedDescription =
            CatalogTranslationService.GetModuleDescription(definition.Id, language);
        if (!string.IsNullOrEmpty(localizedDescription))
            definition.Description = localizedDescription;
    }

    public static void Localize(SettingDefinition definition)
    {
        var language = LocalizationService.CurrentLanguage;
        if (language == "ja-JP") return;

        var originalKeywords = definition.Keywords;
        if (language == "en-US")
        {
            definition.DisplayName = GetSettingName(definition.Id);
            definition.Category = GetCategory(definition.Id);
            definition.Description = $"Open the Windows settings page for {definition.DisplayName}.";
        }
        else
        {
            definition.DisplayName =
                CatalogTranslationService.GetSettingName(definition.Id, language);
            definition.Category = CatalogTranslationService.GetCategory(definition.Id, language);
            definition.Description =
                CatalogTranslationService.GetDescription(definition.DisplayName, language);
        }

        definition.Keywords = originalKeywords
            .Concat(definition.DisplayName.Split([' ', '&', '-', '/'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Concat([definition.Category])
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string GetCategory(string id)
    {
        var prefix = id.Split('.', 2)[0];
        return Categories.TryGetValue(prefix, out var category) ? category : "Windows settings";
    }

    private static string GetSettingName(string id)
    {
        if (SettingNames.TryGetValue(id, out var name)) return name;
        var part = id.Contains('.') ? id[(id.IndexOf('.') + 1)..] : id;
        if (Words.TryGetValue(part, out name)) return name;
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(part.Replace('-', ' '));
    }
}
