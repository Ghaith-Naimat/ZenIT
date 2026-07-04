namespace ZenIT.Core.Configuration;

public sealed record ITPolicy
{
    public const string DefaultITModeUsername = "Ghaith";
    public const string DefaultITModePasswordHash = "95FAB1FCF914BB5E3D56891BD2B1D03B40DD6066D3ED1327798A9673BB0A30FC";
    public const string DefaultContactITUrl = "https://zenhr.slack.com/team/U09CGMUGV6K";

    public bool EnableITMode { get; init; } = true;
    public string ITModeUsername { get; init; } = DefaultITModeUsername;
    public string ITModePasswordHash { get; init; } = DefaultITModePasswordHash;
    public bool AllowITCredentialChanges { get; init; }
    public int ITModeSessionTimeoutMinutes { get; init; } = 15;
    public string ContactITUrl { get; init; } = DefaultContactITUrl;
    public IReadOnlyList<string> AllowedITWorkflows { get; init; } = [];

    public static ITPolicy Disabled => new()
    {
        EnableITMode = false,
        ITModeUsername = string.Empty,
        ITModePasswordHash = string.Empty,
        AllowITCredentialChanges = false,
        ITModeSessionTimeoutMinutes = 15,
        ContactITUrl = DefaultContactITUrl
    };
}
