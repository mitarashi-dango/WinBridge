using System.Globalization;
using System.Text.Json;

namespace WinBridge.Localization;

public static class LocalizationService
{
    private static IReadOnlyDictionary<string, string> _translations =
        new Dictionary<string, string>();
    private static readonly string WindowsLanguage = CultureInfo.CurrentUICulture.Name;
    private static readonly HashSet<string> SupportedLanguages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ja-JP", "en-US", "zh-CN", "zh-TW", "es-ES"
        };

    public static string CurrentLanguage { get; private set; } = "ja-JP";
    public static string LanguagePreference { get; private set; } = "system";
    public static bool UsesTranslationResource => CurrentLanguage != "ja-JP";

    public static void Initialize(string? language)
    {
        LanguagePreference = NormalizePreference(language);
        CurrentLanguage = ResolveLanguage(LanguagePreference, WindowsLanguage);

        var culture = CultureInfo.GetCultureInfo(CurrentLanguage);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        if (!UsesTranslationResource)
        {
            _translations = new Dictionary<string, string>();
            return;
        }

        try
        {
            var path = Path.Combine(
                AppContext.BaseDirectory, "Resources", $"Strings.{CurrentLanguage}.json");
            var json = File.ReadAllText(path);
            _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                            ?? new Dictionary<string, string>();
        }
        catch
        {
            _translations = new Dictionary<string, string>();
        }
    }

    public static string Translate(string? source)
    {
        if (string.IsNullOrEmpty(source) || !UsesTranslationResource) return source ?? "";
        return _translations.TryGetValue(source, out var translated) ? translated : source;
    }

    public static string ResolveLanguage(string? preference, string? windowsLanguage)
    {
        var normalized = NormalizePreference(preference);
        if (normalized != "system") return normalized;
        if (string.IsNullOrWhiteSpace(windowsLanguage)) return "en-US";
        if (windowsLanguage.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja-JP";
        if (windowsLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return "es-ES";
        if (IsTraditionalChinese(windowsLanguage)) return "zh-TW";
        if (windowsLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        return "en-US";
    }

    private static string NormalizePreference(string? language)
    {
        if (SupportedLanguages.Contains(language ?? ""))
            return SupportedLanguages.First(item =>
                string.Equals(item, language, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(language, "zh-Hans", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (string.Equals(language, "zh-Hant", StringComparison.OrdinalIgnoreCase)) return "zh-TW";
        return "system";
    }

    private static bool IsTraditionalChinese(string language) =>
        language.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
        language.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
        language.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
        language.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase);
}

public static class L
{
    public static string T(string? source) => LocalizationService.Translate(source);

    public static string F(string source, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(source), args);
}
