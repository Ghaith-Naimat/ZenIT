using System.Threading;

namespace ZenIT.Core.Workflows;

public static class WorkflowExecutionConsent
{
    private static readonly AsyncLocal<HashSet<WorkflowId>?> ConfirmedWorkflows = new();

    public static bool HasConsent(WorkflowId workflowId)
    {
        return ConfirmedWorkflows.Value?.Contains(workflowId) == true;
    }

    public static IDisposable Grant(WorkflowId workflowId)
    {
        ConfirmedWorkflows.Value ??= [];
        ConfirmedWorkflows.Value.Add(workflowId);
        return new ConsentScope(workflowId);
    }

    private sealed class ConsentScope(WorkflowId workflowId) : IDisposable
    {
        public void Dispose()
        {
            ConfirmedWorkflows.Value?.Remove(workflowId);
        }
    }
}
