namespace ZenIT.Core.Configuration;

public static class ThemeManager
{
    public const string CurrentTheme = "Dark";

    private static readonly HashSet<string> SupportedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dark",
        "Light",
        "HighContrast"
    };

    public static bool IsSupported(string? theme)
    {
        return !string.IsNullOrWhiteSpace(theme) && SupportedThemes.Contains(theme);
    }

    public static string Normalize(string? theme)
    {
        return IsSupported(theme) ? theme! : CurrentTheme;
    }
}
