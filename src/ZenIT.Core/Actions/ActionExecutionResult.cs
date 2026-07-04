namespace ZenIT.Core.Actions;

public sealed record ActionExecutionResult(
    ActionId ActionId,
    bool Success,
    string UserMessage,
    string TechnicalMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string? ReportPath = null);
