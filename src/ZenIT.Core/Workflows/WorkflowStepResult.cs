namespace ZenIT.Core.Workflows;

public sealed record WorkflowStepResult(
    string StepName,
    bool Success,
    string TechnicalMessage,
    TimeSpan Duration);
