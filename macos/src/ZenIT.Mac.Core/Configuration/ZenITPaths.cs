namespace ZenIT.Core.Configuration;

/// <summary>
/// macOS storage layout for managed deployment. Policy is machine-wide and protected,
/// while logs and reports are shared locations writable by employee sessions.
/// </summary>
public static class ZenITPaths
{
    private static readonly ZenITPathProvider DefaultProvider = ZenITPathProvider.CreateProduction();

    public static readonly string Root = DefaultProvider.Root;
    public static readonly string ConfigDirectory = DefaultProvider.ConfigDirectory;
    public static readonly string PolicyDirectory = DefaultProvider.PolicyDirectory;
    public static readonly string LogsDirectory = DefaultProvider.LogsDirectory;
    public static readonly string ReportsDirectory = DefaultProvider.ReportsDirectory;
    public static readonly string SettingsPath = Path.Combine(ConfigDirectory, "appsettings.json");
    public static readonly string ITPolicyPath = Path.Combine(PolicyDirectory, "itpolicy.json");
    public static readonly string LogPath = Path.Combine(LogsDirectory, "ZenIT.log");
}

public sealed class ZenITPathProvider
{
    public ZenITPathProvider(string root, string logsDirectory, string reportsDirectory)
    {
        Root = root;
        ConfigDirectory = Path.Combine(root, "Config");
        PolicyDirectory = Path.Combine(root, "Policy");
        LogsDirectory = logsDirectory;
        ReportsDirectory = reportsDirectory;
        SettingsPath = Path.Combine(ConfigDirectory, "appsettings.json");
        ITPolicyPath = Path.Combine(PolicyDirectory, "itpolicy.json");
        LogPath = Path.Combine(LogsDirectory, "ZenIT.log");
    }

    public string Root { get; }
    public string ConfigDirectory { get; }
    public string PolicyDirectory { get; }
    public string LogsDirectory { get; }
    public string ReportsDirectory { get; }
    public string SettingsPath { get; }
    public string ITPolicyPath { get; }
    public string LogPath { get; }

    public static ZenITPathProvider CreateProduction()
    {
        var root = Path.Combine(
            Path.DirectorySeparatorChar.ToString(),
            "Library",
            "Application Support",
            "ZenIT");
        var logs = Path.Combine(
            Path.DirectorySeparatorChar.ToString(),
            "Library",
            "Logs",
            "ZenIT");
        var reports = Path.Combine(
            Path.DirectorySeparatorChar.ToString(),
            "Users",
            "Shared",
            "ZenIT",
            "Reports");

        return new ZenITPathProvider(root, logs, reports);
    }

    public static ZenITPathProvider CreateForTest(string testRoot)
    {
        return new ZenITPathProvider(
            Path.Combine(testRoot, "ApplicationSupport", "ZenIT"),
            Path.Combine(testRoot, "Logs", "ZenIT"),
            Path.Combine(testRoot, "Reports"));
    }
}
