namespace ZenIT.Core.Workflows;

public sealed record WorkflowDefinition(
    WorkflowId Id,
    string Title,
    string Description,
    string ButtonText,
    string IconCode,
    string Category,
    WorkflowRiskLevel RiskLevel,
    WorkflowAccessTier AccessTier,
    bool RequiresITMode,
    bool RequiresConfirmation,
    bool RequiresAdmin,
    bool IsRecommended,
    bool IsSupportAction,
    int TimeoutSeconds);
