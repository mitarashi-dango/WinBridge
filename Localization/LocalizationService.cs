using System.Globalization;
using System.Text.Json;

namespace WinBridge.Localization;

public static class LocalizationService
{
    private static IReadOnlyDictionary<string, string> _english = new Dictionary<string, string>();
    private static readonly string WindowsLanguage = CultureInfo.CurrentUICulture.Name;

    public static string CurrentLanguage { get; private set; } = "ja-JP";
    public static string LanguagePreference { get; private set; } = "system";
    public static bool IsEnglish => CurrentLanguage == "en-US";

    public static void Initialize(string? language)
    {
        LanguagePreference = NormalizePreference(language);
        CurrentLanguage = ResolveLanguage(LanguagePreference, WindowsLanguage);

        var culture = CultureInfo.GetCultureInfo(CurrentLanguage);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        if (!IsEnglish)
        {
            _english = new Dictionary<string, string>();
            return;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Strings.en-US.json");
            var json = File.ReadAllText(path);
            _english = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
        }
        catch
        {
            _english = new Dictionary<string, string>();
        }
    }

    public static string Translate(string? source)
    {
        if (string.IsNullOrEmpty(source) || !IsEnglish) return source ?? "";
        return _english.TryGetValue(source, out var translated) ? translated : source;
    }

    public static string ResolveLanguage(string? preference, string? windowsLanguage)
    {
        var normalized = NormalizePreference(preference);
        if (normalized != "system") return normalized;
        return windowsLanguage?.StartsWith("ja", StringComparison.OrdinalIgnoreCase) == true
            ? "ja-JP"
            : "en-US";
    }

    private static string NormalizePreference(string? language)
    {
        if (string.Equals(language, "ja-JP", StringComparison.OrdinalIgnoreCase)) return "ja-JP";
        if (string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase)) return "en-US";
        return "system";
    }
}

public static class L
{
    public static string T(string? source) => LocalizationService.Translate(source);

    public static string F(string source, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(source), args);
}
