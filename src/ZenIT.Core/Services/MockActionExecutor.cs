using ZenIT.Core.Actions;

namespace ZenIT.Core.Services;

public sealed class MockActionExecutor : IActionExecutor
{
    public async Task<ActionExecutionResult> ExecuteAsync(ActionId actionId, CancellationToken cancellationToken = default)
    {
        var definition = ActionRegistry.GetRequired(actionId);
        var startedAt = DateTimeOffset.Now;

        await Task.Delay(1_600, cancellationToken);

        // Future integration point: execute only this registered action definition
        // through signed scripts or a Windows service broker. Never accept arbitrary
        // command text or user-provided script paths.
        var finishedAt = DateTimeOffset.Now;
        return new ActionExecutionResult(
            actionId,
            Success: true,
            UserMessage: "Success. You are all set.",
            TechnicalMessage: $"Mock execution completed for registered action '{definition.Id}'.",
            startedAt,
            finishedAt);
    }
}
