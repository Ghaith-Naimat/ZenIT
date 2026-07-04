namespace ZenIT.Core.Workflows;

public sealed record WorkflowExecutionRequest(
    WorkflowId WorkflowId,
    DateTimeOffset QueuedAt,
    string Source);
