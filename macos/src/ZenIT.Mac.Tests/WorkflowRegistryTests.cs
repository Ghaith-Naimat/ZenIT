using ZenIT.Core.Workflows;

namespace ZenIT.Mac.Tests;

public sealed class WorkflowRegistryTests
{
    [Fact]
    public void WorkflowRegistry_HasExpectedIntegrity()
    {
        Assert.Empty(WorkflowIntegrityValidator.Validate());
        Assert.Equal(13, WorkflowRegistry.EmployeeWorkflows.Count);
        Assert.Equal(18, WorkflowRegistry.ITWorkflows.Count);
        Assert.Equal(WorkflowRegistry.SupportedWorkflows.Count, WorkflowRegistry.SupportedWorkflows.Select(workflow => workflow.Id).Distinct().Count());
    }

    [Fact]
    public void EmployeeWorkflows_DoNotRequireAdminOrITMode()
    {
        Assert.All(WorkflowRegistry.EmployeeWorkflows, workflow =>
        {
            Assert.False(workflow.RequiresAdmin);
            Assert.False(workflow.RequiresITMode);
            Assert.Equal(WorkflowAccessTier.Employee, workflow.AccessTier);
        });
    }

    [Fact]
    public void AdminWorkflows_AreNotLowRisk()
    {
        Assert.All(WorkflowRegistry.ITWorkflows.Where(workflow => workflow.RequiresAdmin), workflow =>
        {
            Assert.NotEqual(WorkflowRiskLevel.Low, workflow.RiskLevel);
            Assert.True(workflow.RequiresConfirmation);
        });
    }

    [Fact]
    public void NoWindowsOnlyWorkflows_AreRegisteredOnMac()
    {
        Assert.DoesNotContain(WorkflowRegistry.SupportedWorkflows, workflow =>
            workflow.Title.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            workflow.Description.Contains("Windows", StringComparison.OrdinalIgnoreCase));
    }
}
