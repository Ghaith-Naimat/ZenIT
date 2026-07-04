namespace ZenIT.Core.Configuration;

public sealed record AppSettings
{
    public string AppMode { get; init; } = "Pilot";
    public string ITSupportEmail { get; init; } = "it@zenhr.com";
    public string CompanyName { get; init; } = "ZenHR";
    public string UpdateChannel { get; init; } = "Production";
    public string Language { get; init; } = "en";
    public string Theme { get; init; } = "Dark";
    public bool EnableExperimentalActions { get; init; }
    public bool EnableTestMode { get; init; }
    public int LogRetentionDays { get; init; } = 30;
    public int ReportRetentionDays { get; init; } = 14;
}
