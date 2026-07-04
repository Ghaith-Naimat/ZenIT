using ZenIT.Core.Workflows;

namespace ZenIT.Core.Services;

public interface IWorkflowExecutionQueue
{
    Task<WorkflowExecutionResult> EnqueueAsync(WorkflowExecutionRequest request, CancellationToken cancellationToken = default);
}
