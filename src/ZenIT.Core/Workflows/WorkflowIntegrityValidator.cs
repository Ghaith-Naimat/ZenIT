namespace ZenIT.Core.Workflows;

public static class WorkflowIntegrityValidator
{
    private static readonly IReadOnlySet<string> EmployeeCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Recommended",
        "Connectivity",
        "Performance",
        "Meetings",
        "Productivity",
        "Printing",
        "Security",
        "My Device",
        "Support"
    };

    private static readonly IReadOnlySet<string> ITCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Diagnostics",
        "Windows Repair",
        "Network Repair",
        "Services",
        "Performance",
        "Updates",
        "Reports"
    };

    public static IReadOnlyList<string> Validate()
    {
        var issues = new List<string>();
        var workflows = WorkflowRegistry.SupportedWorkflows;
        var duplicateIds = workflows
            .GroupBy(workflow => workflow.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        issues.AddRange(duplicateIds.Select(id => $"Duplicate workflow registration: {id}"));

        var registeredIds = workflows.Select(workflow => workflow.Id).ToHashSet();
        foreach (var id in Enum.GetValues<WorkflowId>())
        {
            if (!registeredIds.Contains(id))
            {
                issues.Add($"Workflow enum value is not registered: {id}");
            }
        }

        foreach (var workflow in workflows)
        {
            if (string.IsNullOrWhiteSpace(workflow.Title))
            {
                issues.Add($"{workflow.Id}: title is empty.");
            }

            if (workflow.TimeoutSeconds <= 0)
            {
                issues.Add($"{workflow.Id}: timeout must be positive.");
            }

            if (workflow.AccessTier == WorkflowAccessTier.Employee)
            {
                if (workflow.RequiresITMode)
                {
                    issues.Add($"{workflow.Id}: employee workflow cannot require IT Mode.");
                }

                if (!EmployeeCategories.Contains(workflow.Category))
                {
                    issues.Add($"{workflow.Id}: employee category '{workflow.Category}' is not approved.");
                }
            }
            else
            {
                if (!workflow.RequiresITMode)
                {
                    issues.Add($"{workflow.Id}: IT workflow must require IT Mode.");
                }

                if (!workflow.RequiresConfirmation)
                {
                    issues.Add($"{workflow.Id}: IT workflow must require confirmation.");
                }

                if (!ITCategories.Contains(workflow.Category))
                {
                    issues.Add($"{workflow.Id}: IT category '{workflow.Category}' is not approved.");
                }
            }

            if (workflow.RequiresAdmin && workflow.RiskLevel == WorkflowRiskLevel.Low)
            {
                issues.Add($"{workflow.Id}: admin workflow should not be Low risk.");
            }
        }

        return issues;
    }
}
