using ZenIT.Core.Workflows;

namespace ZenIT.Core.Services;

public interface IWorkflowExecutor
{
    Task<WorkflowExecutionResult> ExecuteAsync(WorkflowId workflowId, CancellationToken cancellationToken = default);
}
