namespace ZenIT.Core.Actions;

public static class ActionRegistry
{
    private static readonly IReadOnlyDictionary<ActionId, ActionDefinition> DefinitionsById =
        new Dictionary<ActionId, ActionDefinition>
        {
            [ActionId.FixInternet] = Create(
                ActionId.FixInternet,
                "Fix Internet",
                "Refresh your connection and check if you are online.",
                "Fix now",
                ActionRiskLevel.Low,
                60),
            [ActionId.FixZoom] = Create(
                ActionId.FixZoom,
                "Fix Zoom",
                "Fix common meeting issues with Zoom.",
                "Fix Zoom",
                ActionRiskLevel.Low,
                90),
            [ActionId.FixSlack] = Create(
                ActionId.FixSlack,
                "Fix Slack",
                "Refresh Slack when messages, calls, or notifications are stuck.",
                "Fix Slack",
                ActionRiskLevel.Low,
                90),
            [ActionId.FixChrome] = Create(
                ActionId.FixChrome,
                "Fix Chrome",
                "Clean common browser issues safely.",
                "Fix Chrome",
                ActionRiskLevel.Low,
                90),
            [ActionId.FixGoogleDrive] = Create(
                ActionId.FixGoogleDrive,
                "Fix Google Drive",
                "Refresh Google Drive sync when files are not updating.",
                "Fix Drive",
                ActionRiskLevel.Low,
                30),
            [ActionId.DeviceHealthCheck] = Create(
                ActionId.DeviceHealthCheck,
                "Device Health Check",
                "Check your device basics and create a support report.",
                "Run check",
                ActionRiskLevel.Low,
                60),
            [ActionId.RequestITHelp] = Create(
                ActionId.RequestITHelp,
                "Request IT Help",
                "Prepare a support package for IT when the issue still happens.",
                "Prepare help request",
                ActionRiskLevel.Low,
                60),
            [ActionId.FixCamera] = Create(
                ActionId.FixCamera,
                "Fix Camera",
                "Help when your camera is not detected in meetings.",
                "Check camera",
                ActionRiskLevel.Low,
                30),
            [ActionId.FixMicrophone] = Create(
                ActionId.FixMicrophone,
                "Fix Microphone",
                "Help when others cannot hear you in meetings.",
                "Check mic",
                ActionRiskLevel.Low,
                30),
            [ActionId.RestartHelper] = Create(
                ActionId.RestartHelper,
                "Restart Helper",
                "Guide a safe restart when your device feels stuck.",
                "Restart help",
                ActionRiskLevel.Low,
                30)
        };

    public static IReadOnlyList<ActionDefinition> SupportedActions { get; } = DefinitionsById.Values.ToList();

    public static ActionDefinition GetRequired(ActionId actionId)
    {
        if (!DefinitionsById.TryGetValue(actionId, out var definition))
        {
            throw new InvalidOperationException($"Action '{actionId}' is not registered.");
        }

        return definition;
    }

    private static ActionDefinition Create(
        ActionId id,
        string title,
        string description,
        string buttonText,
        ActionRiskLevel riskLevel,
        int timeoutSeconds)
    {
        return new ActionDefinition(
            id,
            title,
            description,
            buttonText,
            riskLevel,
            RequiresAdmin: false,
            ScriptPath: null,
            TimeoutSeconds: timeoutSeconds);
    }
}
