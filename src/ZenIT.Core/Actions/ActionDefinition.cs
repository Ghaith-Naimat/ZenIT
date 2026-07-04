namespace ZenIT.Core.Actions;

public sealed record ActionDefinition(
    ActionId Id,
    string Title,
    string Description,
    string ButtonText,
    ActionRiskLevel RiskLevel,
    bool RequiresAdmin,
    string? ScriptPath,
    int TimeoutSeconds);
