using System.Reflection;
using ZenIT.Core.Services;
using ZenIT.Core.Workflows;

namespace ZenIT.Tests;

public sealed class WorkflowOutcomeConsistencyTests
{
    [Fact]
    public void SuccessfulWorkflow_WithOptionalFailedVerification_ReturnsSuccess()
    {
        var outcome = DetermineOutcome(
            success: true,
            needsSupport: false,
            [new WorkflowStepResult("Verify optional app restart", false, "Optional verification failed", TimeSpan.FromMilliseconds(10))]);

        Assert.Equal(WorkflowOutcome.Success, outcome);
    }

    [Fact]
    public void UnsuccessfulWorkflow_WithFailedVerification_ReturnsCannotVerify()
    {
        var outcome = DetermineOutcome(
            success: false,
            needsSupport: false,
            [new WorkflowStepResult("Verify network", false, "Verification failed", TimeSpan.FromMilliseconds(10))]);

        Assert.Equal(WorkflowOutcome.CannotVerify, outcome);
    }

    [Fact]
    public void NeedsSupport_TakesPriorityOverVerification()
    {
        var outcome = DetermineOutcome(
            success: false,
            needsSupport: true,
            [new WorkflowStepResult("Verify app restart", false, "Verification failed", TimeSpan.FromMilliseconds(10))]);

        Assert.Equal(WorkflowOutcome.NeedsIT, outcome);
    }

    private static WorkflowOutcome DetermineOutcome(bool success, bool needsSupport, IReadOnlyList<WorkflowStepResult> steps)
    {
        var method = typeof(LocalWorkflowExecutor).GetMethod("DetermineOutcome", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (WorkflowOutcome)method.Invoke(null, [success, needsSupport, steps])!;
    }
}
