namespace ZenIT.Core.Workflows;

public sealed record WorkflowExecutionResult(
    WorkflowId WorkflowId,
    bool Success,
    bool NeedsITSupport,
    WorkflowOutcome Outcome,
    string UserMessage,
    string TechnicalMessage,
    string? ReportPath,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<WorkflowStepResult> Steps);
