namespace ZenIT.Core.Services;

/// <summary>
/// Central definitions of the managed applications ZenIT for Mac can inspect, refresh, and restart.
/// Process names match the executable names inside each .app bundle; candidates cover the
/// system-wide and per-user Applications folders.
/// </summary>
public static class MacApplicationProfiles
{
    public static ApplicationProcessProfile Chrome { get; } = new(
        "Chrome",
        ["Google Chrome", "Google Chrome Helper"],
        BundleCandidates("Google Chrome.app"));

    public static ApplicationProcessProfile Slack { get; } = new(
        "Slack",
        ["Slack", "Slack Helper"],
        BundleCandidates("Slack.app"));

    public static ApplicationProcessProfile Zoom { get; } = new(
        "Zoom",
        ["zoom.us", "ZoomClips", "caphost"],
        BundleCandidates("zoom.us.app"));

    public static ApplicationProcessProfile GoogleDrive { get; } = new(
        "Google Drive",
        ["Google Drive", "GoogleDriveFS", "FinderSyncAPIExtension"],
        BundleCandidates("Google Drive.app"));

    public static ApplicationProcessProfile JumpCloud { get; } = new(
        "JumpCloud",
        ["jumpcloud-agent", "jcagent", "JumpCloud"],
        []);

    private static IReadOnlyCollection<string> BundleCandidates(string bundleName)
    {
        return
        [
            Path.Combine("/Applications", bundleName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", bundleName)
        ];
    }
}
