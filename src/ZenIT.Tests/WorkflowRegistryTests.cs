using ZenIT.Core.Workflows;

namespace ZenIT.Tests;

public sealed class WorkflowRegistryTests
{
    [Fact]
    public void WorkflowRegistry_HasExpectedIntegrity()
    {
        Assert.Empty(WorkflowIntegrityValidator.Validate());
        Assert.Equal(13, WorkflowRegistry.EmployeeWorkflows.Count);
        Assert.Equal(21, WorkflowRegistry.ITWorkflows.Count);
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
    public void RemovedPrinterWorkflow_IsNotVisibleToEmployees()
    {
        Assert.DoesNotContain(WorkflowRegistry.EmployeeWorkflows, workflow =>
            workflow.Title.Contains("Printer", StringComparison.OrdinalIgnoreCase) ||
            workflow.Id.ToString().Contains("Printer", StringComparison.OrdinalIgnoreCase));
    }
}
