using ZenIT.Core.Workflows;

namespace ZenIT.Core.Services;

public sealed class ImmediateWorkflowExecutionQueue : IWorkflowExecutionQueue
{
    private readonly IWorkflowExecutor _executor;

    public ImmediateWorkflowExecutionQueue(IWorkflowExecutor executor)
    {
        _executor = executor;
    }

    public Task<WorkflowExecutionResult> EnqueueAsync(WorkflowExecutionRequest request, CancellationToken cancellationToken = default)
    {
        WorkflowRegistry.GetRequired(request.WorkflowId);
        return _executor.ExecuteAsync(request.WorkflowId, cancellationToken);
    }
}
