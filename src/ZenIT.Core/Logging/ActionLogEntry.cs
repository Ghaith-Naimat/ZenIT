namespace ZenIT.Core.Logging;

public sealed record ActionLogEntry(
    DateTimeOffset Timestamp,
    string WindowsUsername,
    string DeviceName,
    string ActionId,
    string ActionName,
    string Result,
    string? TechnicalMessage = null,
    string? ReportPath = null,
    string? ErrorMessage = null,
    TimeSpan? Duration = null);
