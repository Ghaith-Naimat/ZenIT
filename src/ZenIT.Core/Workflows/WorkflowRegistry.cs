namespace ZenIT.Core.Workflows;

public static class WorkflowRegistry
{
    private static readonly IReadOnlyDictionary<WorkflowId, WorkflowDefinition> DefinitionsById =
        new Dictionary<WorkflowId, WorkflowDefinition>
        {
            [WorkflowId.FixEverythingSafe] = CreateEmployee(WorkflowId.FixEverythingSafe, "Fix Everything", "Run safe repairs and checks in one click.", "Fix Everything", "SAFE", "Recommended", true, false, 240),
            [WorkflowId.InternetNotWorking] = CreateEmployee(WorkflowId.InternetNotWorking, "Fix Internet", "Repair common internet and Wi-Fi issues.", "Fix Internet", "NET", "Connectivity", false, false, 120),
            [WorkflowId.ImproveDevicePerformance] = CreateEmployee(WorkflowId.ImproveDevicePerformance, "Speed Up Device", "Clean temporary files and improve performance.", "Speed Up Device", "SPD", "Performance", true, false, 180),
            [WorkflowId.ChromeNotWorking] = CreateEmployee(WorkflowId.ChromeNotWorking, "Fix Chrome", "Fix common browser loading and cache issues.", "Fix Chrome", "CHR", "Productivity", false, false, 90),
            [WorkflowId.SlackNotWorking] = CreateEmployee(WorkflowId.SlackNotWorking, "Fix Slack", "Refresh Slack when messages or calls are stuck.", "Fix Slack", "SLK", "Productivity", false, false, 90),
            [WorkflowId.ZoomNotWorking] = CreateEmployee(WorkflowId.ZoomNotWorking, "Fix Zoom", "Refresh Zoom meeting issues.", "Fix Zoom", "ZOM", "Meetings", false, false, 90),
            [WorkflowId.GoogleDriveNotSyncing] = CreateEmployee(WorkflowId.GoogleDriveNotSyncing, "Fix Google Drive", "Check Drive sync and connection issues.", "Fix Google Drive", "DRV", "Productivity", false, false, 60),
            [WorkflowId.CameraOrMicrophoneNotWorking] = CreateEmployee(WorkflowId.CameraOrMicrophoneNotWorking, "Fix Camera & Mic", "Check meeting camera and microphone issues.", "Fix Camera & Mic", "AV", "Meetings", false, false, 60),
            [WorkflowId.SoundNotWorking] = CreateEmployee(WorkflowId.SoundNotWorking, "Fix Sound", "Check speaker and headset issues.", "Fix Sound", "SND", "Meetings", false, false, 60),
            [WorkflowId.SecurityCheck] = CreateEmployee(WorkflowId.SecurityCheck, "Security Check", "Check device protection status.", "Security Check", "SEC", "Security", false, false, 60),
            [WorkflowId.DeviceHealthCheck] = CreateEmployee(WorkflowId.DeviceHealthCheck, "Check My Device", "Review basic device health.", "Check My Device", "HLT", "My Device", true, false, 60),
            [WorkflowId.CollectITReport] = CreateEmployee(WorkflowId.CollectITReport, "Create Support Package", "Prepare files IT can use to troubleshoot faster.", "Create Support Package", "PKG", "Support", false, true, 120),
            [WorkflowId.ContactIT] = CreateEmployee(WorkflowId.ContactIT, "Contact IT", "Open IT support in Slack.", "Contact IT", "IT", "Support", false, true, 30),
            [WorkflowId.FullWindowsRepairCheck] = CreateIT(WorkflowId.FullWindowsRepairCheck, "Full Windows Repair Check", "Run a diagnostic-only Windows repair readiness check.", "Run Check", "CHK", "Diagnostics", WorkflowRiskLevel.Medium, false, true, 120),
            [WorkflowId.ItFlushDns] = CreateIT(WorkflowId.ItFlushDns, "Flush DNS", "Clear the local DNS resolver cache.", "Flush DNS", "DNS", "Network Repair", WorkflowRiskLevel.Low, false, true, 60),
            [WorkflowId.ItReleaseRenewIp] = CreateIT(WorkflowId.ItReleaseRenewIp, "Release & Renew IP", "Refresh IP assignment using ipconfig.", "Renew IP", "IP", "Network Repair", WorkflowRiskLevel.Medium, false, true, 120),
            [WorkflowId.RestartNetworkAdapter] = CreateIT(WorkflowId.RestartNetworkAdapter, "Restart Network Adapter", "Restart the active network adapter when elevated tooling is available.", "Restart Adapter", "NIC", "Network Repair", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.DnsRepair] = CreateIT(WorkflowId.DnsRepair, "DNS Repair", "Validate DNS and prepare fallback DNS guidance.", "Repair DNS", "DNS", "Network Repair", WorkflowRiskLevel.Medium, true, true, 120),
            [WorkflowId.SfcScan] = CreateIT(WorkflowId.SfcScan, "SFC Scan", "Run System File Checker to repair protected Windows files.", "Run SFC", "SFC", "Windows Repair", WorkflowRiskLevel.High, true, true, 1800),
            [WorkflowId.DismScanHealth] = CreateIT(WorkflowId.DismScanHealth, "DISM ScanHealth", "Scan the Windows component store health.", "Scan DISM", "DSM", "Windows Repair", WorkflowRiskLevel.Medium, true, true, 1800),
            [WorkflowId.DismHealthRestore] = CreateIT(WorkflowId.DismHealthRestore, "DISM Health Restore", "Repair the Windows component store.", "Run DISM", "DSM", "Windows Repair", WorkflowRiskLevel.High, true, true, 3600),
            [WorkflowId.WindowsUpdateRepair] = CreateIT(WorkflowId.WindowsUpdateRepair, "Windows Update Repair", "Create a Windows Update diagnostic report.", "Diagnose Updates", "UPD", "Updates", WorkflowRiskLevel.Medium, true, true, 600),
            [WorkflowId.ItTempCleanup] = CreateIT(WorkflowId.ItTempCleanup, "Temp Cleanup", "Clean safe temporary locations and measure reclaimed space.", "Clean Temp", "TMP", "Performance", WorkflowRiskLevel.Medium, false, true, 180),
            [WorkflowId.StartupAnalysis] = CreateIT(WorkflowId.StartupAnalysis, "Startup Analysis", "Review startup load indicators without changing startup apps.", "Analyze Startup", "STA", "Performance", WorkflowRiskLevel.Low, false, true, 120),
            [WorkflowId.RestartWindowsUpdate] = CreateIT(WorkflowId.RestartWindowsUpdate, "Restart Windows Update", "Restart the Windows Update service.", "Restart Update", "WUA", "Services", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.RestartBits] = CreateIT(WorkflowId.RestartBits, "Restart BITS", "Restart the Background Intelligent Transfer Service.", "Restart BITS", "BITS", "Services", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.RestartPrintSpooler] = CreateIT(WorkflowId.RestartPrintSpooler, "Restart Print Spooler", "Restart the Windows printing service.", "Restart Service", "PRN", "Services", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.RestartAudioServices] = CreateIT(WorkflowId.RestartAudioServices, "Restart Audio Services", "Restart core Windows audio services.", "Restart Audio", "AUD", "Services", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.NetworkStackReset] = CreateIT(WorkflowId.NetworkStackReset, "Network Stack Reset", "Reset TCP/IP network stack. Restart may be required.", "Reset Stack", "NET", "Network Repair", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.WinsockReset] = CreateIT(WorkflowId.WinsockReset, "Winsock Reset", "Reset Winsock catalog. Restart is required.", "Reset Winsock", "WSK", "Network Repair", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.WingetUpgradeAll] = CreateIT(WorkflowId.WingetUpgradeAll, "Winget Upgrade All", "Check and run available winget application upgrades.", "Run Upgrades", "WNG", "Updates", WorkflowRiskLevel.High, false, true, 3600),
            [WorkflowId.AdvancedEventReport] = CreateIT(WorkflowId.AdvancedEventReport, "Advanced Event Report", "Export recent Windows critical and warning event summaries.", "Export Events", "EVT", "Reports", WorkflowRiskLevel.Low, false, true, 180),
            [WorkflowId.ServiceHealthRepair] = CreateIT(WorkflowId.ServiceHealthRepair, "Service Health Repair", "List failed or stopped automatic services without repairing them.", "Check Services", "SVC", "Services", WorkflowRiskLevel.Medium, false, true, 180),
            [WorkflowId.ExportAdvancedDiagnosticPackage] = CreateIT(WorkflowId.ExportAdvancedDiagnosticPackage, "Export Advanced Diagnostic Package", "Create an advanced IT diagnostic package without private data.", "Export Package", "PKG", "Reports", WorkflowRiskLevel.Medium, false, true, 300)
        };

    public static IReadOnlyList<WorkflowDefinition> SupportedWorkflows { get; } = DefinitionsById.Values.ToList();
    public static IReadOnlyList<WorkflowDefinition> EmployeeWorkflows { get; } = SupportedWorkflows.Where(workflow => workflow.AccessTier == WorkflowAccessTier.Employee).ToList();
    public static IReadOnlyList<WorkflowDefinition> ITWorkflows { get; } = SupportedWorkflows.Where(workflow => workflow.AccessTier == WorkflowAccessTier.IT).ToList();

    public static WorkflowDefinition GetRequired(WorkflowId workflowId)
    {
        if (!DefinitionsById.TryGetValue(workflowId, out var definition))
        {
            throw new InvalidOperationException($"Workflow '{workflowId}' is not registered.");
        }

        return definition;
    }

    private static WorkflowDefinition CreateEmployee(
        WorkflowId id,
        string title,
        string description,
        string buttonText,
        string iconCode,
        string category,
        bool isRecommended,
        bool isSupportAction,
        int timeoutSeconds,
        bool requiresConfirmation = false)
    {
        return Create(id, title, description, buttonText, iconCode, category, WorkflowRiskLevel.Low, WorkflowAccessTier.Employee, false, requiresConfirmation, false, isRecommended, isSupportAction, timeoutSeconds);
    }

    private static WorkflowDefinition CreateIT(
        WorkflowId id,
        string title,
        string description,
        string buttonText,
        string iconCode,
        string category,
        WorkflowRiskLevel riskLevel,
        bool requiresAdmin,
        bool requiresConfirmation,
        int timeoutSeconds)
    {
        return Create(id, title, description, buttonText, iconCode, category, riskLevel, WorkflowAccessTier.IT, true, requiresConfirmation, requiresAdmin, false, false, timeoutSeconds);
    }

    private static WorkflowDefinition Create(
        WorkflowId id,
        string title,
        string description,
        string buttonText,
        string iconCode,
        string category,
        WorkflowRiskLevel riskLevel,
        WorkflowAccessTier accessTier,
        bool requiresITMode,
        bool requiresConfirmation,
        bool requiresAdmin,
        bool isRecommended,
        bool isSupportAction,
        int timeoutSeconds)
    {
        return new WorkflowDefinition(
            id,
            title,
            description,
            buttonText,
            iconCode,
            category,
            riskLevel,
            accessTier,
            requiresITMode,
            requiresConfirmation,
            requiresAdmin,
            isRecommended,
            isSupportAction,
            timeoutSeconds);
    }
}
