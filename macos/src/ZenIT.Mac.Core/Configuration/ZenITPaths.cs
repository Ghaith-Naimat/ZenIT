namespace ZenIT.Core.Configuration;

/// <summary>
/// macOS storage layout. ZenIT for Mac runs without elevation, so all state lives in the
/// per-user Application Support folder instead of a machine-wide location.
/// </summary>
public static class ZenITPaths
{
    public static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Application Support",
        "ZenIT");

    public static readonly string ConfigDirectory = Path.Combine(Root, "Config");
    public static readonly string PolicyDirectory = Path.Combine(Root, "Policy");
    public static readonly string LogsDirectory = Path.Combine(Root, "Logs");
    public static readonly string ReportsDirectory = Path.Combine(Root, "Reports");
    public static readonly string SettingsPath = Path.Combine(ConfigDirectory, "appsettings.json");
    public static readonly string ITPolicyPath = Path.Combine(PolicyDirectory, "itpolicy.json");
    public static readonly string LogPath = Path.Combine(LogsDirectory, "ZenIT.log");
}
