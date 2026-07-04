using ZenIT.Core.Actions;

namespace ZenIT.Core.Services;

public interface IActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(ActionId actionId, CancellationToken cancellationToken = default);
}
