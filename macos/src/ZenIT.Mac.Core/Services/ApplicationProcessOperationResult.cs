namespace ZenIT.Core.Services;

public sealed record ApplicationProcessOperationResult(
    bool Success,
    string TechnicalMessage,
    TimeSpan Duration);

