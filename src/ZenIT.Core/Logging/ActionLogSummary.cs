namespace ZenIT.Core.Logging;

public sealed record ActionLogSummary(
    DateTimeOffset Timestamp,
    string ActionId,
    string ActionName,
    string Result,
    TimeSpan? Duration = null,
    string? ReportPath = null);
