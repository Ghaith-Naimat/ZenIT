namespace ZenIT.Core.Configuration;

/// <summary>
/// macOS storage layout for managed deployment. Policy is machine-wide and protected,
/// while logs and reports are shared locations writable by employee sessions.
/// </summary>
public static class ZenITPaths
{
    private static readonly string MachineRoot = OperatingSystem.IsMacOS()
        ? Path.DirectorySeparatorChar.ToString()
        : Path.Combine(Path.GetTempPath(), "ZenIT-Mac-TestRoot");

    public static readonly string Root = Path.Combine(
        MachineRoot,
        "Library",
        "Application Support",
        "ZenIT");

    public static readonly string ConfigDirectory = Path.Combine(Root, "Config");
    public static readonly string PolicyDirectory = Path.Combine(Root, "Policy");
    public static readonly string LogsDirectory = Path.Combine(
        MachineRoot,
        "Library",
        "Logs",
        "ZenIT");
    public static readonly string ReportsDirectory = Path.Combine(
        MachineRoot,
        "Users",
        "Shared",
        "ZenIT",
        "Reports");
    public static readonly string SettingsPath = Path.Combine(ConfigDirectory, "appsettings.json");
    public static readonly string ITPolicyPath = Path.Combine(PolicyDirectory, "itpolicy.json");
    public static readonly string LogPath = Path.Combine(LogsDirectory, "ZenIT.log");
}
