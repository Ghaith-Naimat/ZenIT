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
            [WorkflowId.FullMacRepairCheck] = CreateIT(WorkflowId.FullMacRepairCheck, "Full macOS Repair Check", "Run a diagnostic-only macOS repair readiness check.", "Run Check", "CHK", "Diagnostics", WorkflowRiskLevel.Medium, false, true, 120),
            [WorkflowId.ItFlushDns] = CreateIT(WorkflowId.ItFlushDns, "Flush DNS Cache", "Clear the local DNS resolver cache.", "Flush DNS", "DNS", "Network Repair", WorkflowRiskLevel.Low, false, true, 60),
            [WorkflowId.RenewDhcpLease] = CreateIT(WorkflowId.RenewDhcpLease, "Renew DHCP Lease", "Request a fresh IP address for the active network interface.", "Renew Lease", "IP", "Network Repair", WorkflowRiskLevel.Medium, true, true, 120),
            [WorkflowId.RestartWifi] = CreateIT(WorkflowId.RestartWifi, "Restart Wi-Fi", "Power-cycle the Wi-Fi interface when elevated tooling is available.", "Restart Wi-Fi", "NIC", "Network Repair", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.DnsRepair] = CreateIT(WorkflowId.DnsRepair, "DNS Repair", "Validate DNS and prepare fallback DNS guidance.", "Repair DNS", "DNS", "Network Repair", WorkflowRiskLevel.Medium, true, true, 120),
            [WorkflowId.VerifyStartupDisk] = CreateIT(WorkflowId.VerifyStartupDisk, "Verify Startup Disk", "Run First Aid verification on the startup volume.", "Verify Disk", "DSK", "macOS Repair", WorkflowRiskLevel.High, true, true, 1800),
            [WorkflowId.SystemIntegrityCheck] = CreateIT(WorkflowId.SystemIntegrityCheck, "System Integrity Check", "Review SIP, Gatekeeper, and FileVault protection status.", "Run Check", "SIP", "macOS Repair", WorkflowRiskLevel.Medium, false, true, 300),
            [WorkflowId.SpotlightReindex] = CreateIT(WorkflowId.SpotlightReindex, "Rebuild Spotlight Index", "Erase and rebuild the Spotlight search index.", "Reindex", "SPT", "macOS Repair", WorkflowRiskLevel.High, true, true, 600),
            [WorkflowId.SoftwareUpdateCheck] = CreateIT(WorkflowId.SoftwareUpdateCheck, "Software Update Check", "List available macOS software updates.", "Check Updates", "UPD", "Updates", WorkflowRiskLevel.Medium, false, true, 600),
            [WorkflowId.SoftwareUpdateInstallAll] = CreateIT(WorkflowId.SoftwareUpdateInstallAll, "Install All Updates", "Download and install all available macOS updates.", "Install Updates", "UPD", "Updates", WorkflowRiskLevel.High, true, true, 3600),
            [WorkflowId.ItTempCleanup] = CreateIT(WorkflowId.ItTempCleanup, "Temp Cleanup", "Clean safe temporary locations and measure reclaimed space.", "Clean Temp", "TMP", "Performance", WorkflowRiskLevel.Medium, false, true, 180),
            [WorkflowId.StartupAnalysis] = CreateIT(WorkflowId.StartupAnalysis, "Startup Analysis", "Review launch agents and daemons without changing them.", "Analyze Startup", "STA", "Performance", WorkflowRiskLevel.Low, false, true, 120),
            [WorkflowId.RestartCoreAudio] = CreateIT(WorkflowId.RestartCoreAudio, "Restart Core Audio", "Restart the macOS audio engine (coreaudiod).", "Restart Audio", "AUD", "Services", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.RestartPrintingSystem] = CreateIT(WorkflowId.RestartPrintingSystem, "Restart Printing System", "Restart the CUPS printing service.", "Restart Service", "PRN", "Services", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.RestartMDnsResponder] = CreateIT(WorkflowId.RestartMDnsResponder, "Restart DNS Responder", "Reload mDNSResponder to clear stuck name resolution.", "Restart DNS", "DNS", "Services", WorkflowRiskLevel.High, true, true, 120),
            [WorkflowId.AdvancedEventReport] = CreateIT(WorkflowId.AdvancedEventReport, "Advanced Event Report", "Export recent crash and diagnostic report summaries.", "Export Events", "EVT", "Reports", WorkflowRiskLevel.Low, false, true, 180),
            [WorkflowId.ServiceHealthRepair] = CreateIT(WorkflowId.ServiceHealthRepair, "Service Health Check", "List failed launch services without repairing them.", "Check Services", "SVC", "Services", WorkflowRiskLevel.Medium, false, true, 180),
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
