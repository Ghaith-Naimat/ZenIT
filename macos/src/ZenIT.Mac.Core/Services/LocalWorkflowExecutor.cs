using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ZenIT.Core.Configuration;
using ZenIT.Core.Execution;
using ZenIT.Core.Logging;
using ZenIT.Core.Models;
using ZenIT.Core.Reports;
using ZenIT.Core.Workflows;

namespace ZenIT.Core.Services;

/// <summary>
/// macOS workflow implementations. Mirrors the Windows executor's safety model:
/// employee workflows are diagnostic or cache-safe only, IT workflows run only
/// registered commands, and admin-only repairs stop safely when not elevated.
/// </summary>
public sealed class LocalWorkflowExecutor : IWorkflowExecutor
{
    private readonly ProcessRunner _processRunner;
    private readonly DeviceHealthService _deviceHealthService;
    private readonly LogService _logService;
    private readonly AppSettings _settings;
    private readonly ITPolicy _itPolicy;

    public LocalWorkflowExecutor(
        ProcessRunner processRunner,
        DeviceHealthService deviceHealthService,
        LogService logService,
        AppSettings settings,
        ITPolicy? itPolicy = null)
    {
        _processRunner = processRunner;
        _deviceHealthService = deviceHealthService;
        _logService = logService;
        _settings = settings;
        _itPolicy = itPolicy ?? ITPolicy.Disabled;
    }

    public async Task<WorkflowExecutionResult> ExecuteAsync(WorkflowId workflowId, CancellationToken cancellationToken = default)
    {
        WorkflowRegistry.GetRequired(workflowId);
        var startedAt = DateTimeOffset.Now;
        var steps = new List<WorkflowStepResult>();

        return workflowId switch
        {
            WorkflowId.InternetNotWorking => await InternetNotWorkingAsync(startedAt, steps, cancellationToken),
            WorkflowId.FixEverythingSafe => await FixEverythingSafeAsync(startedAt, steps, cancellationToken),
            WorkflowId.ImproveDevicePerformance => ImproveDevicePerformance(startedAt, steps),
            WorkflowId.ChromeNotWorking => ChromeNotWorking(startedAt, steps),
            WorkflowId.CameraOrMicrophoneNotWorking => MeetingDevices(startedAt, steps),
            WorkflowId.SoundNotWorking => SoundNotWorking(startedAt, steps),
            WorkflowId.SlackNotWorking => SlackNotWorking(startedAt, steps),
            WorkflowId.ZoomNotWorking => ZoomNotWorking(startedAt, steps),
            WorkflowId.GoogleDriveNotSyncing => GoogleDriveNotSyncing(startedAt, steps),
            WorkflowId.SecurityCheck => SecurityCheck(startedAt, steps),
            WorkflowId.DeviceHealthCheck => DeviceHealthCheck(startedAt, steps),
            WorkflowId.CollectITReport => CollectITReport(startedAt, steps),
            WorkflowId.ContactIT => ContactIT(startedAt, steps),
            WorkflowId.FullMacRepairCheck => FullMacRepairCheck(startedAt, steps),
            WorkflowId.ItFlushDns => await ItFlushDnsAsync(startedAt, steps, cancellationToken),
            WorkflowId.RenewDhcpLease => await RenewDhcpLeaseAsync(startedAt, steps, cancellationToken),
            WorkflowId.RestartWifi => await RestartWifiAsync(startedAt, steps, cancellationToken),
            WorkflowId.DnsRepair => DnsRepair(startedAt, steps),
            WorkflowId.VerifyStartupDisk => await RunAdminCommandWorkflowAsync(WorkflowId.VerifyStartupDisk, "Startup disk verification completed.", "diskutil", "verifyVolume /", TimeSpan.FromMinutes(30), startedAt, steps, cancellationToken),
            WorkflowId.SystemIntegrityCheck => await SystemIntegrityCheckAsync(startedAt, steps, cancellationToken),
            WorkflowId.SoftwareUpdateCheck => await SoftwareUpdateCheckAsync(startedAt, steps, cancellationToken),
            WorkflowId.SoftwareUpdateInstallAll => await RunAdminCommandWorkflowAsync(WorkflowId.SoftwareUpdateInstallAll, "Software updates completed. A restart may be required.", "softwareupdate", "-i -a", TimeSpan.FromMinutes(60), startedAt, steps, cancellationToken),
            WorkflowId.ItTempCleanup => ItTempCleanup(startedAt, steps),
            WorkflowId.StartupAnalysis => StartupAnalysis(startedAt, steps),
            WorkflowId.RestartCoreAudio => await RunAdminCommandWorkflowAsync(WorkflowId.RestartCoreAudio, "Core Audio restart completed.", "killall", "coreaudiod", TimeSpan.FromMinutes(2), startedAt, steps, cancellationToken),
            WorkflowId.RestartPrintingSystem => await RunAdminCommandWorkflowAsync(WorkflowId.RestartPrintingSystem, "Printing system restart completed.", "launchctl", "kickstart -k system/org.cups.cupsd", TimeSpan.FromMinutes(2), startedAt, steps, cancellationToken),
            WorkflowId.RestartMDnsResponder => await RunAdminCommandWorkflowAsync(WorkflowId.RestartMDnsResponder, "DNS responder restart completed.", "killall", "-HUP mDNSResponder", TimeSpan.FromMinutes(2), startedAt, steps, cancellationToken),
            WorkflowId.SpotlightReindex => await RunAdminCommandWorkflowAsync(WorkflowId.SpotlightReindex, "Spotlight reindex started. Indexing continues in the background.", "mdutil", "-E /", TimeSpan.FromMinutes(10), startedAt, steps, cancellationToken),
            WorkflowId.AdvancedEventReport => AdvancedEventReport(startedAt, steps),
            WorkflowId.ServiceHealthRepair => ServiceHealthRepair(startedAt, steps),
            WorkflowId.ExportAdvancedDiagnosticPackage => ExportAdvancedDiagnosticPackage(startedAt, steps),
            _ => throw new InvalidOperationException($"Workflow '{workflowId}' is not registered.")
        };
    }

    /// <summary>
    /// Diagnoses network state before repair, runs only the safe command needed, and verifies gateway, DNS, and internet access.
    /// Employee Mode never renews DHCP leases or power-cycles interfaces because those require elevated tooling on macOS.
    /// </summary>
    private async Task<WorkflowExecutionResult> InternetNotWorkingAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        var enabledAdapters = 0;
        var disabledAdapters = 0;
        RunStep(steps, "Detect Wi-Fi and Ethernet adapters", () =>
        {
            var adapters = GetPhysicalNetworkAdapterSummaries();
            enabledAdapters = adapters.Count(adapter => adapter.Enabled);
            disabledAdapters = adapters.Count(adapter => !adapter.Enabled);
            return (adapters.Count > 0, $"Adapters={string.Join("; ", adapters.Select(adapter => $"{adapter.Name}:{adapter.Type}:{adapter.Status}"))}; Enabled={enabledAdapters}; Disabled={disabledAdapters}");
        });

        if (enabledAdapters == 0 && disabledAdapters > 0)
        {
            AddStep(steps, "Enable disabled adapter", false, "RequiresAdmin=True; EmployeeModeDoesNotEnableAdapters=True");
            return CreateResult(WorkflowId.InternetNotWorking, false, "Network adapter is disabled. Please contact IT.", true, startedAt, steps);
        }

        if (enabledAdapters == 0)
        {
            AddStep(steps, "Check connected network", false, "NoEnabledWifiOrEthernetAdapter=True");
            return CreateResult(WorkflowId.InternetNotWorking, false, "No network connection detected. Please contact IT.", true, startedAt, steps);
        }

        var connectedNetwork = RunStep(steps, "Check connected network", () => (NetworkInterface.GetIsNetworkAvailable(), $"NetworkAvailable={NetworkInterface.GetIsNetworkAvailable()}"));
        var hasIp = RunStep(steps, "Diagnose IP configuration", () =>
        {
            var ips = GetIPv4Addresses();
            return (ips.Count > 0, $"IPv4={string.Join(",", ips)}");
        });

        var hasDnsServers = RunStep(steps, "Diagnose DNS servers", () =>
        {
            var dnsServers = GetDnsServers();
            return (dnsServers.Count > 0, $"DnsServers={string.Join(",", dnsServers)}");
        });

        var gatewayOk = await VerifyGatewayAsync(steps, "Diagnose gateway", cancellationToken);
        var internetOk = await VerifyInternetAsync(steps, "Diagnose internet access", cancellationToken);
        var dnsOk = VerifyDns(steps, "Diagnose DNS resolution");
        CheckProxyAndVpn(steps);

        if (connectedNetwork && hasIp && gatewayOk && internetOk && dnsOk)
        {
            AddStep(steps, "Verify internet repair", true, "NoRepairNeeded=True; Gateway=True; Internet=True; Dns=True");
            return CreateResult(WorkflowId.InternetNotWorking, true, "Internet connection restored.", false, startedAt, steps);
        }

        if (!hasIp || !gatewayOk)
        {
            AddStep(steps, "Repair IP configuration - renew DHCP lease", false, "RequiresAdmin=True; EmployeeModeDoesNotRenewDhcp=True; Skipped=True");
            gatewayOk = await VerifyGatewayAsync(steps, "Verify gateway after diagnosis", cancellationToken);
        }

        if (!hasDnsServers || !dnsOk)
        {
            await RunProcessStepAsync(steps, "Repair DNS - flush cache", "dscacheutil", "-flushcache", cancellationToken);
            dnsOk = VerifyDns(steps, "Verify DNS after refresh");
        }

        internetOk = await VerifyInternetAsync(steps, "Verify internet after repair", cancellationToken);

        if (connectedNetwork && hasIp && gatewayOk && internetOk && dnsOk)
        {
            return CreateResult(WorkflowId.InternetNotWorking, true, "Internet connection restored.", false, startedAt, steps);
        }

        if (!dnsOk)
        {
            AddStep(steps, "Repair network stack", false, "RequiresITMode=True; InterfaceResetSkipped=True; EmployeeModeDoesNotResetInterfaces=True");
        }

        if (!connectedNetwork)
        {
            return CreateResult(WorkflowId.InternetNotWorking, false, "No network connection detected. Please contact IT.", true, startedAt, steps);
        }

        if (!gatewayOk)
        {
            return CreateResult(WorkflowId.InternetNotWorking, false, "Network issue detected. Please contact IT.", true, startedAt, steps);
        }

        if (!dnsOk || !internetOk)
        {
            return CreateResult(WorkflowId.InternetNotWorking, false, "Internet issue detected. Please contact IT.", true, startedAt, steps);
        }

        return CreateResult(WorkflowId.InternetNotWorking, false, "Internet repair was attempted but ZenIT could not verify the result.", false, startedAt, steps);
    }

    /// <summary>
    /// Checks CPU/RAM/disk/uptime and clears safe temporary/cache locations only.
    /// It does not touch Documents, Desktop, Downloads, browser passwords/history, login items, or unrelated processes.
    /// </summary>
    private WorkflowExecutionResult ImproveDevicePerformance(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var driveBefore = GetSystemDriveFreeBytes();
        RunStep(steps, "Check CPU usage", () =>
        {
            var cpuUsage = MacSystemInfo.GetCpuUsageEstimate();
            return (true, cpuUsage.HasValue ? $"CpuUsagePercent={cpuUsage.Value:0.0}" : $"ProcessCount={Process.GetProcesses().Length}");
        });
        var memoryLow = false;
        RunStep(steps, "Check RAM usage", () =>
        {
            var memory = MacSystemInfo.GetMemoryStatus();
            memoryLow = memory.AvailablePercent < 15;
            return (!memoryLow, $"AvailableMemoryPercent={memory.AvailablePercent:0.0}; TotalMemory={FormatBytes((long)memory.TotalBytes)}");
        });

        var diskLow = false;
        RunStep(steps, "Check disk free space", () =>
        {
            var drive = new DriveInfo("/");
            diskLow = drive.AvailableFreeSpace < 10L * 1024L * 1024L * 1024L ||
                      drive.AvailableFreeSpace / (double)drive.TotalSize < 0.10;
            return (!diskLow, $"Free={FormatBytes(drive.AvailableFreeSpace)}; Total={FormatBytes(drive.TotalSize)}");
        });

        var restartRecommended = false;
        RunStep(steps, "Check uptime", () =>
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            restartRecommended = uptime.TotalDays >= 7;
            return (!restartRecommended, $"Uptime={FormatUptime(uptime)}");
        });
        ClearTempFolder(steps, Path.GetTempPath());
        ClearCachePaths(steps, "UserCaches", [GetUserLibraryPath("Caches")]);
        AddStep(steps, "Trash cleanup", true, "Skipped=True; EmployeeModeDoesNotEmptyTrash=True");

        var reclaimedBytes = Math.Max(0, GetSystemDriveFreeBytes() - driveBefore);
        return CreateResult(
            WorkflowId.ImproveDevicePerformance,
            true,
            $"Optimization complete. Recovered: {FormatBytes(reclaimedBytes)}.",
            false,
            startedAt,
            steps);
    }

    private async Task<WorkflowExecutionResult> FixEverythingSafeAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        AppendNestedResult(steps, DeviceHealthCheck(startedAt, []));
        await AppendNestedResultAsync(steps, InternetNotWorkingAsync(startedAt, [], cancellationToken));
        await RunProcessStepAsync(steps, "Flush DNS", "dscacheutil", "-flushcache", cancellationToken);
        RunStep(steps, "Check disk space", () =>
        {
            var drive = new DriveInfo("/");
            return (drive.AvailableFreeSpace > 10L * 1024L * 1024L * 1024L, $"Free={FormatBytes(drive.AvailableFreeSpace)}; Total={FormatBytes(drive.TotalSize)}");
        });
        AppendNestedResult(steps, ImproveDevicePerformance(startedAt, []));
        AppendNestedResult(steps, ChromeNotWorking(startedAt, []));
        AppendNestedResult(steps, SoundNotWorking(startedAt, []));
        AppendNestedResult(steps, MeetingDevices(startedAt, []));
        AppendNestedResult(steps, GoogleDriveStatusForFixEverything(startedAt, []));
        AppendNestedResult(steps, SecurityCheck(startedAt, []));
        var snapshot = _deviceHealthService.GetCurrentHealth();
        AddStep(steps, "Create health snapshot", true, $"Device={snapshot.DeviceName}; Internet={snapshot.InternetConnectivityStatus}; DiskFree={FormatBytes(snapshot.FreeDiskSpaceBytes)}");
        var skippedAppRefresh = steps.Any(step => step.TechnicalMessage.Contains("SkippedBecauseAppRunning=True", StringComparison.OrdinalIgnoreCase));
        return CreateResult(
            WorkflowId.FixEverythingSafe,
            true,
            skippedAppRefresh ? "Device optimization completed successfully. Some app refresh steps were skipped because apps are open." : "Device optimization completed successfully.",
            false,
            startedAt,
            steps);
    }

    /// <summary>
    /// Performs meeting-device diagnostics only: detected camera/microphone hardware and meeting app process state.
    /// Camera and microphone privacy permissions are managed by macOS (TCC) and are never read or changed.
    /// </summary>
    private WorkflowExecutionResult MeetingDevices(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        RunStep(steps, "Check camera privacy permission", () => (true, "CameraPermission=ManagedByMacOSPrivacySettings"));
        RunStep(steps, "Check microphone privacy permission", () => (true, "MicrophonePermission=ManagedByMacOSPrivacySettings"));
        var cameraDeviceHint = RunStep(steps, "Check camera devices", () =>
        {
            var count = GetCameraDeviceCount();
            return (count > 0, $"CameraDevicesDetected={count}");
        });
        var microphoneDeviceHint = RunStep(steps, "Check microphone devices", () =>
        {
            var count = GetAudioInputDeviceCount();
            return (count > 0, $"AudioInputDevicesDetected={count}");
        });
        RunStep(steps, "Check Zoom process state", () => (true, $"ZoomRunning={MacSystemInfo.IsAnyProcessRunning(MacApplicationProfiles.Zoom.ProcessNames)}"));
        RunStep(steps, "Check Chrome process state", () => (true, $"ChromeRunning={MacSystemInfo.IsAnyProcessRunning(MacApplicationProfiles.Chrome.ProcessNames)}"));
        var needsSupport = !cameraDeviceHint || !microphoneDeviceHint;

        return CreateResult(
            WorkflowId.CameraOrMicrophoneNotWorking,
            !needsSupport,
            needsSupport ? "Meeting device issue detected. Please contact IT." : "Meeting device check completed.",
            needsSupport,
            startedAt,
            steps);
    }

    /// <summary>
    /// Performs audio diagnostics only: Core Audio engine state and detected input/output devices.
    /// It does not restart services or change default speaker/microphone settings.
    /// </summary>
    private WorkflowExecutionResult SoundNotWorking(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var needsSupport = false;
        RunStep(steps, "Check Core Audio service status", () =>
        {
            var running = MacSystemInfo.IsAnyProcessRunning(["coreaudiod"]);
            needsSupport = !running;
            return (running, $"CoreAudioRunning={running}");
        });
        RunStep(steps, "Check audio output devices", () =>
        {
            var renderCount = GetAudioOutputDeviceCount();
            needsSupport |= renderCount == 0;
            return (renderCount > 0, $"AudioOutputDevicesDetected={renderCount}");
        });
        RunStep(steps, "Check audio input devices", () =>
        {
            var captureCount = GetAudioInputDeviceCount();
            return (captureCount > 0, $"AudioInputDevicesDetected={captureCount}");
        });

        return CreateResult(
            WorkflowId.SoundNotWorking,
            !needsSupport,
            needsSupport ? "Audio issue detected. Please contact IT." : "Sound check completed.",
            needsSupport,
            startedAt,
            steps);
    }

    /// <summary>
    /// Refreshes Chrome automatically using only Chrome's allowlisted processes and safe current-user cache folders.
    /// It never deletes bookmarks, history, cookies, saved passwords, profiles, extensions, downloads, or documents.
    /// </summary>
    private WorkflowExecutionResult ChromeNotWorking(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        return RepairApplicationWorkflow(
            WorkflowId.ChromeNotWorking,
            MacApplicationProfiles.Chrome,
            GetChromeCachePaths(),
            "Chrome refresh completed.",
            "Chrome repair was attempted but ZenIT could not verify Chrome restarted.",
            startedAt,
            steps,
            afterRepair: () => RunStep(steps, "Check browser network availability", () => (NetworkInterface.GetIsNetworkAvailable(), "NetworkAvailable")));
    }

    /// <summary>
    /// Refreshes Slack automatically using only Slack's allowlisted processes and safe current-user cache folders.
    /// It does not delete downloads, workspace credentials, or user files.
    /// </summary>
    private WorkflowExecutionResult SlackNotWorking(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        return RepairApplicationWorkflow(
            WorkflowId.SlackNotWorking,
            MacApplicationProfiles.Slack,
            GetSlackCachePaths(),
            "Slack refresh completed.",
            "Slack repair was attempted but ZenIT could not verify Slack restarted.",
            startedAt,
            steps,
            afterRepair: () => RunStep(steps, "Check internet connectivity", () => (NetworkInterface.GetIsNetworkAvailable(), "NetworkAvailable")));
    }

    /// <summary>
    /// Refreshes Zoom automatically using only Zoom's allowlisted processes and safe current-user cache folders.
    /// It does not delete recordings, meeting files, or user documents.
    /// </summary>
    private WorkflowExecutionResult ZoomNotWorking(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        return RepairApplicationWorkflow(
            WorkflowId.ZoomNotWorking,
            MacApplicationProfiles.Zoom,
            GetZoomCachePaths(),
            "Zoom refresh completed.",
            "Zoom repair was attempted but ZenIT could not verify Zoom restarted.",
            startedAt,
            steps,
            afterRepair: () =>
            {
                RunStep(steps, "Check meeting devices", () => (true, "CameraMicrophoneCheck=ReadOnlyGuidance"));
                RunStep(steps, "Check internet connectivity", () => (NetworkInterface.GetIsNetworkAvailable(), "NetworkAvailable"));
            });
    }

    /// <summary>
    /// Refreshes Google Drive sync by restarting only allowlisted Drive processes when Drive was already running.
    /// It does not delete Drive cache, disconnect accounts, list files, or touch synced files.
    /// </summary>
    private WorkflowExecutionResult GoogleDriveNotSyncing(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var profile = MacApplicationProfiles.GoogleDrive;
        var manager = new ApplicationProcessManager(profile);
        var snapshot = manager.GetSnapshot();
        AddStep(steps, "Diagnose Google Drive process state", true, snapshot.TechnicalMessage);
        RunStep(steps, "Check internet connectivity", () => (NetworkInterface.GetIsNetworkAvailable(), "NetworkAvailable"));
        RunStep(steps, "Check disk free space", () =>
        {
            var drive = new DriveInfo("/");
            return (drive.AvailableFreeSpace > 5L * 1024L * 1024L * 1024L, $"Free={FormatBytes(drive.AvailableFreeSpace)}");
        });

        if (!snapshot.IsRunning)
        {
            return CreateResult(WorkflowId.GoogleDriveNotSyncing, true, "Google Drive check completed.", false, startedAt, steps);
        }

        if (!StopApplicationProcesses(steps, manager, profile.DisplayName))
        {
            return CreateResult(WorkflowId.GoogleDriveNotSyncing, false, "Google Drive needs IT support.", true, startedAt, steps);
        }

        AddApplicationStep(steps, $"Restart {profile.DisplayName}", manager.Restart(ProcessWindowStyle.Minimized));
        var verified = AddApplicationStep(steps, $"Verify {profile.DisplayName} restart", manager.VerifyRunning(TimeSpan.FromSeconds(10)));
        return CreateResult(
            WorkflowId.GoogleDriveNotSyncing,
            verified,
            verified ? "Google Drive check completed." : "Google Drive repair was attempted but ZenIT could not verify Drive restarted.",
            !verified,
            startedAt,
            steps);
    }

    private WorkflowExecutionResult GoogleDriveStatusForFixEverything(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var running = new ApplicationProcessManager(MacApplicationProfiles.GoogleDrive).IsRunning();
        AddStep(steps, "Check Google Drive process", true, $"GoogleDriveRunning={running}; RestartSkipped=True");
        RunStep(steps, "Check Google Drive internet dependency", () => (NetworkInterface.GetIsNetworkAvailable(), "NetworkAvailable"));
        return CreateResult(WorkflowId.GoogleDriveNotSyncing, true, "Google Drive status checked.", false, startedAt, steps);
    }

    private WorkflowExecutionResult RepairApplicationWorkflow(
        WorkflowId workflowId,
        ApplicationProcessProfile profile,
        IReadOnlyCollection<string> cachePaths,
        string successMessage,
        string cannotVerifyMessage,
        DateTimeOffset startedAt,
        List<WorkflowStepResult> steps,
        Action? afterRepair = null)
    {
        var manager = new ApplicationProcessManager(profile);
        var snapshot = manager.GetSnapshot();
        AddStep(steps, $"Diagnose {profile.DisplayName} process state", true, snapshot.TechnicalMessage);

        if (snapshot.IsRunning && !StopApplicationProcesses(steps, manager, profile.DisplayName))
        {
            return CreateResult(workflowId, false, $"{profile.DisplayName} needs IT support.", true, startedAt, steps);
        }

        ClearCachePaths(steps, profile.DisplayName, cachePaths);
        afterRepair?.Invoke();

        if (!snapshot.IsRunning)
        {
            AddApplicationStep(steps, $"Verify {profile.DisplayName} remains closed", manager.VerifyStopped(TimeSpan.FromSeconds(2)));
            return CreateResult(workflowId, true, successMessage, false, startedAt, steps);
        }

        AddApplicationStep(steps, $"Restart {profile.DisplayName}", manager.Restart());
        var verified = AddApplicationStep(steps, $"Verify {profile.DisplayName} restart", manager.VerifyRunning(TimeSpan.FromSeconds(10)));
        return CreateResult(workflowId, verified, verified ? successMessage : cannotVerifyMessage, !verified, startedAt, steps);
    }

    private static bool StopApplicationProcesses(List<WorkflowStepResult> steps, ApplicationProcessManager manager, string appName)
    {
        AddApplicationStep(steps, $"Gracefully close {appName}", manager.CloseGracefully(TimeSpan.FromSeconds(5)));
        var stoppedAfterGracefulClose = AddApplicationStep(steps, $"Verify {appName} stopped after graceful close", manager.VerifyStopped(TimeSpan.FromSeconds(1)));
        if (stoppedAfterGracefulClose)
        {
            return true;
        }

        AddApplicationStep(steps, $"Terminate {appName} background processes", manager.ForceTerminate(TimeSpan.FromSeconds(5)));
        return AddApplicationStep(steps, $"Verify {appName} fully stopped", manager.VerifyStopped(TimeSpan.FromSeconds(5)));
    }

    private static bool AddApplicationStep(List<WorkflowStepResult> steps, string stepName, ApplicationProcessOperationResult result)
    {
        steps.Add(new WorkflowStepResult(stepName, result.Success, result.TechnicalMessage, result.Duration));
        return result.Success;
    }

    /// <summary>
    /// Performs read-only endpoint security checks: application firewall state, FileVault, Gatekeeper,
    /// System Integrity Protection, and JumpCloud process hints. It never changes security settings.
    /// </summary>
    private WorkflowExecutionResult SecurityCheck(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var needsSupport = false;
        RunStep(steps, "Check application firewall status", () =>
        {
            var status = GetFirewallStatus();
            var enabled = status.Contains("enabled", StringComparison.OrdinalIgnoreCase);
            needsSupport |= status.Contains("disabled", StringComparison.OrdinalIgnoreCase);
            return (enabled, $"FirewallStatus={status}");
        });
        RunStep(steps, "Check FileVault status", () =>
        {
            var status = GetFileVaultStatus();
            var enabled = status.Contains("FileVault is On", StringComparison.OrdinalIgnoreCase);
            needsSupport |= status.Contains("FileVault is Off", StringComparison.OrdinalIgnoreCase);
            return (enabled, $"FileVaultStatus={status}");
        });
        RunStep(steps, "Check Gatekeeper status", () =>
        {
            var status = GetGatekeeperStatus();
            return (!status.Contains("disabled", StringComparison.OrdinalIgnoreCase), $"GatekeeperStatus={status}");
        });
        RunStep(steps, "Check System Integrity Protection", () =>
        {
            var status = GetSipStatus();
            return (!status.Contains("disabled", StringComparison.OrdinalIgnoreCase), $"SipStatus={status}");
        });
        RunStep(steps, "Check MDM state", () =>
        {
            var jumpCloudDetected = MacSystemInfo.IsAnyProcessRunning(MacApplicationProfiles.JumpCloud.ProcessNames);
            needsSupport |= !jumpCloudDetected;
            return (jumpCloudDetected, $"JumpCloudProcessDetected={jumpCloudDetected}");
        });

        return CreateResult(
            WorkflowId.SecurityCheck,
            !needsSupport,
            needsSupport ? "Security attention needed. Please contact IT." : "Security check completed.",
            needsSupport,
            startedAt,
            steps);
    }

    /// <summary>
    /// Runs DeviceHealthService and writes a small non-sensitive support summary.
    /// </summary>
    private WorkflowExecutionResult DeviceHealthCheck(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var health = _deviceHealthService.GetCurrentHealth();
        AddStep(steps, "Run device health check", true, BuildHealthTechnicalMessage(health));
        var reportPath = WriteDeviceSummary(health);
        AddStep(steps, "Create support summary", true, $"ReportPath={reportPath}");
        return CreateResult(WorkflowId.DeviceHealthCheck, true, "Device health check completed.", false, startedAt, steps, reportPath);
    }

    /// <summary>
    /// Creates TXT, JSON, and HTML support package files with non-sensitive device, network, performance, security,
    /// macOS health, and ZenIT activity summaries. It excludes personal files, browser data, cookies, passwords, tokens,
    /// emails, chats, Google Drive file names, and full installed software inventory.
    /// </summary>
    private WorkflowExecutionResult CollectITReport(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var report = BuildITReportData();
        var paths = WriteITReport(report);
        AddStep(steps, "Collect device details", true, "NonSensitiveDeviceDetailsCollected");
        AddStep(steps, "Create support package", true, $"ReportPaths={string.Join(",", paths.Values)}");
        return CreateResult(WorkflowId.CollectITReport, true, "Support package created.", false, startedAt, steps, paths["txt"]);
    }

    private WorkflowExecutionResult ContactIT(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var contactUrl = string.IsNullOrWhiteSpace(_itPolicy.ContactITUrl) ? ITPolicy.DefaultContactITUrl : _itPolicy.ContactITUrl;
        try
        {
            Process.Start(new ProcessStartInfo(contactUrl) { UseShellExecute = true });
            AddStep(steps, "Open IT Support Slack link", true, $"Url={contactUrl}");
            return CreateResult(WorkflowId.ContactIT, true, "Opening IT Support in Slack.", false, startedAt, steps);
        }
        catch (Exception exception)
        {
            AddStep(steps, "Open IT Support Slack link", false, exception.Message);
            return CreateResult(WorkflowId.ContactIT, false, "Could not open Slack. Please contact IT manually.", true, startedAt, steps);
        }
    }

    private WorkflowExecutionResult FullMacRepairCheck(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        RunStep(steps, "Check uptime", () =>
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return (true, $"Uptime={FormatUptime(uptime)}");
        });
        RunStep(steps, "Check disk verification availability", () => (IsCurrentProcessElevated(), "DiskutilVerifyAvailableWhenElevated"));
        RunStep(steps, "Check System Integrity Protection", () => (true, $"SipStatus={GetSipStatus()}"));
        RunStep(steps, "Check failed launch services", () => (true, string.Join(",", MacSystemInfo.GetFailedLaunchServices())));
        RunStep(steps, "Check recent crash reports", () => (true, $"CrashReportsLast7Days={MacSystemInfo.CountRecentDiagnosticReports(7)}"));
        return CreateResult(WorkflowId.FullMacRepairCheck, true, "macOS repair check completed.", false, startedAt, steps);
    }

    private WorkflowExecutionResult ItTempCleanup(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        AppendNestedResult(steps, ImproveDevicePerformance(startedAt, []));
        return CreateResult(WorkflowId.ItTempCleanup, steps.All(step => step.Success), "Temp cleanup completed.", steps.Any(step => !step.Success), startedAt, steps);
    }

    private WorkflowExecutionResult StartupAnalysis(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        RunStep(steps, "Count launch agents and daemons", () =>
        {
            var userAgents = CountDirectoryEntries(GetUserLibraryPath("LaunchAgents"));
            var systemAgents = CountDirectoryEntries("/Library/LaunchAgents");
            var systemDaemons = CountDirectoryEntries("/Library/LaunchDaemons");
            return (true, $"UserLaunchAgents={userAgents}; SystemLaunchAgents={systemAgents}; SystemLaunchDaemons={systemDaemons}; NoChangesMade=True");
        });
        RunStep(steps, "Check startup load indicator", () => (true, $"RunningProcesses={Process.GetProcesses().Length}"));
        return CreateResult(WorkflowId.StartupAnalysis, true, "Startup analysis completed.", false, startedAt, steps);
    }

    private async Task<WorkflowExecutionResult> ItFlushDnsAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        await RunProcessStepAsync(steps, "Flush DNS cache", "dscacheutil", "-flushcache", cancellationToken);
        if (IsCurrentProcessElevated())
        {
            await RunControlledProcessStepAsync(steps, "Reload DNS responder", "killall", "-HUP mDNSResponder", TimeSpan.FromMinutes(1), cancellationToken);
        }
        else
        {
            AddStep(steps, "Reload DNS responder", true, "RequiresAdmin=True; Skipped=True; FlushCacheStillApplied=True");
        }

        return CreateResult(WorkflowId.ItFlushDns, steps.All(step => step.Success), "DNS cache flushed.", !steps.All(step => step.Success), startedAt, steps);
    }

    private async Task<WorkflowExecutionResult> RenewDhcpLeaseAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        if (!IsCurrentProcessElevated())
        {
            AddStep(steps, "Check administrator permissions", false, "RequiresAdmin=True");
            return CreateResult(WorkflowId.RenewDhcpLease, false, "This repair requires administrator permissions.", true, startedAt, steps);
        }

        var primaryInterface = GetPrimaryInterfaceName();
        if (primaryInterface is null)
        {
            AddStep(steps, "Detect primary network interface", false, "PrimaryInterface=NotFound");
            return CreateResult(WorkflowId.RenewDhcpLease, false, "No active network interface detected.", true, startedAt, steps);
        }

        AddStep(steps, "Detect primary network interface", true, $"PrimaryInterface={primaryInterface}");
        await RunControlledProcessStepAsync(steps, "Renew DHCP lease", "ipconfig", $"set {primaryInterface} DHCP", TimeSpan.FromMinutes(1), cancellationToken);
        return CreateResult(WorkflowId.RenewDhcpLease, steps.All(step => step.Success), "DHCP lease renewed.", !steps.All(step => step.Success), startedAt, steps);
    }

    private async Task<WorkflowExecutionResult> RestartWifiAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        if (!IsCurrentProcessElevated())
        {
            AddStep(steps, "Check administrator permissions", false, "RequiresAdmin=True");
            return CreateResult(WorkflowId.RestartWifi, false, "This repair requires administrator permissions.", true, startedAt, steps);
        }

        var wifiDevice = GetWifiDeviceName();
        if (wifiDevice is null)
        {
            AddStep(steps, "Detect Wi-Fi interface", false, "WifiInterface=NotFound");
            return CreateResult(WorkflowId.RestartWifi, false, "No Wi-Fi interface detected.", true, startedAt, steps);
        }

        AddStep(steps, "Detect Wi-Fi interface", true, $"WifiInterface={wifiDevice}");
        await RunControlledProcessStepAsync(steps, "Turn Wi-Fi off", "networksetup", $"-setairportpower {wifiDevice} off", TimeSpan.FromMinutes(1), cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        await RunControlledProcessStepAsync(steps, "Turn Wi-Fi on", "networksetup", $"-setairportpower {wifiDevice} on", TimeSpan.FromMinutes(1), cancellationToken);
        return CreateResult(WorkflowId.RestartWifi, steps.All(step => step.Success), "Wi-Fi restart completed.", !steps.All(step => step.Success), startedAt, steps);
    }

    private WorkflowExecutionResult DnsRepair(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        RunStep(steps, "Validate DNS servers", () =>
        {
            var servers = GetDnsServers();
            return (servers.Count > 0, $"DnsServers={string.Join(",", servers)}; FallbackDns=8.8.8.8,1.1.1.1");
        });
        RunStep(steps, "Validate DNS resolution", () =>
        {
            var addresses = Dns.GetHostAddresses("zenhr.com");
            return (addresses.Length > 0, $"zenhr.com={addresses.Length} addresses");
        });
        AddStep(steps, "Prepare fallback DNS guidance", IsCurrentProcessElevated(), "Changing DNS requires approved elevated IT tooling.");
        return CreateResult(WorkflowId.DnsRepair, steps.Take(2).All(step => step.Success), "DNS repair check completed.", steps.Any(step => !step.Success), startedAt, steps);
    }

    private async Task<WorkflowExecutionResult> SystemIntegrityCheckAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        await RunControlledProcessStepAsync(steps, "Check System Integrity Protection", "csrutil", "status", TimeSpan.FromMinutes(1), cancellationToken);
        await RunControlledProcessStepAsync(steps, "Check Gatekeeper", "spctl", "--status", TimeSpan.FromMinutes(1), cancellationToken);
        await RunControlledProcessStepAsync(steps, "Check FileVault", "fdesetup", "status", TimeSpan.FromMinutes(1), cancellationToken);
        return CreateResult(WorkflowId.SystemIntegrityCheck, steps.All(step => step.Success), "System integrity check completed.", false, startedAt, steps);
    }

    private async Task<WorkflowExecutionResult> SoftwareUpdateCheckAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        await RunControlledProcessStepAsync(steps, "Check available software updates", "softwareupdate", "-l", TimeSpan.FromMinutes(10), cancellationToken);
        return CreateResult(WorkflowId.SoftwareUpdateCheck, true, "Software update check completed.", false, startedAt, steps);
    }

    private WorkflowExecutionResult AdvancedEventReport(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var report = BuildITReportData();
        report["AdvancedEvents"] = $"CrashReportsLast7Days={MacSystemInfo.CountRecentDiagnosticReports(7)}";
        var paths = WriteITReport(report, "AdvancedEventReport");
        AddStep(steps, "Export advanced event report", true, $"ReportPaths={string.Join(",", paths.Values)}");
        return CreateResult(WorkflowId.AdvancedEventReport, true, "Advanced event report exported.", false, startedAt, steps, paths["txt"]);
    }

    private WorkflowExecutionResult ServiceHealthRepair(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        AddStep(steps, "List failed launch services", true, string.Join(",", MacSystemInfo.GetFailedLaunchServices()));
        return CreateResult(WorkflowId.ServiceHealthRepair, true, "Service health check completed.", false, startedAt, steps);
    }

    private WorkflowExecutionResult ExportAdvancedDiagnosticPackage(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var report = BuildITReportData();
        report["AdvancedDiagnostics"] = "Advanced package excludes private content and personal files.";
        var paths = WriteITReport(report, "AdvancedDiagnosticPackage");
        AddStep(steps, "Export advanced diagnostic package", true, $"ReportPaths={string.Join(",", paths.Values)}");
        return CreateResult(WorkflowId.ExportAdvancedDiagnosticPackage, true, "Advanced diagnostic package exported.", false, startedAt, steps, paths["txt"]);
    }

    private async Task RunProcessStepAsync(List<WorkflowStepResult> steps, string stepName, string fileName, string arguments, CancellationToken cancellationToken)
    {
        await RunStepAsync(steps, stepName, async () =>
        {
            var result = await _processRunner.RunAsync(fileName, arguments, TimeSpan.FromSeconds(30), cancellationToken);
            return (result.ExitCode == 0, $"{result.FileName} {result.Arguments}; ExitCode={result.ExitCode}; Error={TrimForLog(result.StandardError)}");
        });
    }

    private static Task<bool> VerifyGatewayAsync(List<WorkflowStepResult> steps, string stepName, CancellationToken cancellationToken)
    {
        return RunStepAsync(steps, stepName, async () =>
        {
            var gateway = GetDefaultGateway();
            if (gateway is null)
            {
                return (false, "DefaultGateway=NotFound");
            }

            return (await PingHostAsync(gateway.ToString(), cancellationToken), $"Gateway={gateway}");
        });
    }

    private static Task<bool> VerifyInternetAsync(List<WorkflowStepResult> steps, string stepName, CancellationToken cancellationToken)
    {
        return RunStepAsync(steps, stepName, async () =>
        {
            return (await PingHostAsync("8.8.8.8", cancellationToken), "Target=8.8.8.8");
        });
    }

    private static bool VerifyDns(List<WorkflowStepResult> steps, string stepName)
    {
        return RunStep(steps, stepName, () =>
        {
            var addresses = Dns.GetHostAddresses("zenhr.com");
            return (addresses.Length > 0, $"zenhr.com={addresses.Length} addresses");
        });
    }

    private static void CheckProxyAndVpn(List<WorkflowStepResult> steps)
    {
        RunStep(steps, "Diagnose proxy settings", () =>
        {
            var proxies = MacSystemInfo.RunCapture("/usr/sbin/scutil", "--proxies") ?? string.Empty;
            var httpEnabled = Regex.Match(proxies, @"HTTPEnable\s*:\s*(\d)");
            var httpsEnabled = Regex.Match(proxies, @"HTTPSEnable\s*:\s*(\d)");
            return (true, $"HTTPProxyEnabled={(httpEnabled.Success ? httpEnabled.Groups[1].Value : "Unknown")}; HTTPSProxyEnabled={(httpsEnabled.Success ? httpsEnabled.Groups[1].Value : "Unknown")}");
        });

        RunStep(steps, "Diagnose VPN state", () =>
        {
            var vpnInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Count(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up &&
                                           networkInterface.Name.StartsWith("utun", StringComparison.OrdinalIgnoreCase));
            var vpnProcesses = MacSystemInfo.CountProcessesContaining("vpn");
            return (true, $"VpnLikeInterfaces={vpnInterfaces}; VpnLikeProcesses={vpnProcesses}");
        });
    }

    private static bool RunStep(List<WorkflowStepResult> steps, string stepName, Func<(bool Success, string TechnicalMessage)> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = action();
            stopwatch.Stop();
            steps.Add(new WorkflowStepResult(stepName, result.Success, result.TechnicalMessage, stopwatch.Elapsed));
            return result.Success;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            steps.Add(new WorkflowStepResult(stepName, false, exception.Message, stopwatch.Elapsed));
            return false;
        }
    }

    private static async Task<bool> RunStepAsync(List<WorkflowStepResult> steps, string stepName, Func<Task<(bool Success, string TechnicalMessage)>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action();
            stopwatch.Stop();
            steps.Add(new WorkflowStepResult(stepName, result.Success, result.TechnicalMessage, stopwatch.Elapsed));
            return result.Success;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            steps.Add(new WorkflowStepResult(stepName, false, exception.Message, stopwatch.Elapsed));
            return false;
        }
    }

    private static void AddStep(List<WorkflowStepResult> steps, string stepName, bool success, string technicalMessage)
    {
        steps.Add(new WorkflowStepResult(stepName, success, technicalMessage, TimeSpan.Zero));
    }

    private static void AppendNestedResult(List<WorkflowStepResult> steps, WorkflowExecutionResult result)
    {
        foreach (var step in result.Steps)
        {
            steps.Add(new WorkflowStepResult($"{result.WorkflowId}: {step.StepName}", step.Success, step.TechnicalMessage, step.Duration));
        }
    }

    private static async Task AppendNestedResultAsync(List<WorkflowStepResult> steps, Task<WorkflowExecutionResult> resultTask)
    {
        AppendNestedResult(steps, await resultTask);
    }

    private WorkflowExecutionResult CreateResult(
        WorkflowId workflowId,
        bool success,
        string userMessage,
        bool needsSupport,
        DateTimeOffset startedAt,
        IReadOnlyList<WorkflowStepResult> steps,
        string? reportPath = null)
    {
        return new WorkflowExecutionResult(
            workflowId,
            success,
            needsSupport,
            DetermineOutcome(success, needsSupport, steps),
            userMessage,
            BuildTechnicalMessage(steps),
            reportPath,
            startedAt,
            DateTimeOffset.Now,
            steps);
    }

    private static WorkflowOutcome DetermineOutcome(bool success, bool needsSupport, IReadOnlyList<WorkflowStepResult> steps)
    {
        if (needsSupport)
        {
            return WorkflowOutcome.NeedsIT;
        }

        if (success)
        {
            return WorkflowOutcome.Success;
        }

        if (steps.Any(step => step.StepName.Contains("Verify", StringComparison.OrdinalIgnoreCase) && !step.Success))
        {
            return WorkflowOutcome.CannotVerify;
        }

        return WorkflowOutcome.RepairAttempted;
    }

    private static string BuildTechnicalMessage(IReadOnlyList<WorkflowStepResult> steps)
    {
        return string.Join(" | ", steps.Select(step => $"{step.StepName}: success={step.Success}; {step.TechnicalMessage}; durationMs={step.Duration.TotalMilliseconds:0}"));
    }

    private static void ClearTempFolder(List<WorkflowStepResult> steps, string folderPath)
    {
        ClearCachePaths(steps, $"Temp:{folderPath}", [folderPath]);
    }

    private static long GetSystemDriveFreeBytes()
    {
        return new DriveInfo("/").AvailableFreeSpace;
    }

    private static void ClearCachePaths(List<WorkflowStepResult> steps, string label, IReadOnlyCollection<string> paths)
    {
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var attempted = 0;
            var deleted = 0;
            long bytesDeleted = 0;
            var skipped = 0;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (!Directory.Exists(path))
                {
                    steps.Add(new WorkflowStepResult($"Clear {label}", true, $"Missing={path}", stopwatch.Elapsed));
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        attempted++;
                        var length = new FileInfo(file).Length;
                        File.Delete(file);
                        deleted++;
                        bytesDeleted += length;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        skipped++;
                    }
                }

                foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(directory => directory.Length))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(directory).Any())
                        {
                            Directory.Delete(directory);
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        skipped++;
                    }
                }

                stopwatch.Stop();
                steps.Add(new WorkflowStepResult($"Clear {label}", true, $"Path={path}; AttemptedFiles={attempted}; DeletedFiles={deleted}; EstimatedSpaceCleared={FormatBytes(bytesDeleted)}; SkippedLocked={skipped}", stopwatch.Elapsed));
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                steps.Add(new WorkflowStepResult($"Clear {label}", false, $"Path={path}; Error={exception.Message}", stopwatch.Elapsed));
            }
        }
    }

    private static string GetUserLibraryPath(params string[] segments)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine([home, "Library", .. segments]);
    }

    private static IReadOnlyCollection<string> GetSlackCachePaths()
    {
        return
        [
            GetUserLibraryPath("Application Support", "Slack", "Cache"),
            GetUserLibraryPath("Application Support", "Slack", "Code Cache"),
            GetUserLibraryPath("Application Support", "Slack", "GPUCache"),
            GetUserLibraryPath("Application Support", "Slack", "Service Worker", "CacheStorage"),
            GetUserLibraryPath("Caches", "com.tinyspeck.slackmacgap")
        ];
    }

    private static IReadOnlyCollection<string> GetChromeCachePaths()
    {
        return
        [
            GetUserLibraryPath("Caches", "Google", "Chrome", "Default", "Cache"),
            GetUserLibraryPath("Caches", "Google", "Chrome", "Default", "Code Cache"),
            GetUserLibraryPath("Application Support", "Google", "Chrome", "Default", "GPUCache"),
            GetUserLibraryPath("Application Support", "Google", "Chrome", "Default", "Service Worker", "CacheStorage")
        ];
    }

    private static IReadOnlyCollection<string> GetZoomCachePaths()
    {
        return
        [
            GetUserLibraryPath("Caches", "us.zoom.xos"),
            GetUserLibraryPath("Logs", "zoom.us")
        ];
    }

    private static IReadOnlyList<string> GetIPv4Addresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsPhysicalCandidate)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(address => address.Address.ToString())
            .ToList();
    }

    private static IReadOnlyList<NetworkAdapterSummary> GetPhysicalNetworkAdapterSummaries()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsPhysicalCandidate)
            .Select(networkInterface => new NetworkAdapterSummary(
                networkInterface.Name,
                networkInterface.NetworkInterfaceType.ToString(),
                networkInterface.OperationalStatus.ToString(),
                networkInterface.OperationalStatus == OperationalStatus.Up))
            .ToList();
    }

    private static bool IsPhysicalCandidate(NetworkInterface networkInterface)
    {
        var name = networkInterface.Name;
        return name.StartsWith("en", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("awdl", StringComparison.OrdinalIgnoreCase) &&
               networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Loopback;
    }

    private static IReadOnlyList<string> GetDnsServers()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().DnsAddresses)
            .Where(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(address => address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IPAddress? GetDefaultGateway()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().GatewayAddresses)
            .Select(gateway => gateway.Address)
            .FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
    }

    private static async Task<bool> PingHostAsync(string host, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(host, 1500);
        cancellationToken.ThrowIfCancellationRequested();
        return reply.Status == IPStatus.Success;
    }

    private static string? GetPrimaryInterfaceName()
    {
        var output = MacSystemInfo.RunCapture("/sbin/route", "-n get default") ?? string.Empty;
        var match = Regex.Match(output, @"interface:\s*(\S+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? GetWifiDeviceName()
    {
        var output = MacSystemInfo.RunCapture("/usr/sbin/networksetup", "-listallhardwareports") ?? string.Empty;
        var match = Regex.Match(output, @"Hardware Port:\s*Wi-Fi\s*\nDevice:\s*(\S+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private Dictionary<string, object?> BuildITReportData()
    {
        var health = _deviceHealthService.GetCurrentHealth();
        var drive = new DriveInfo("/");
        var memory = MacSystemInfo.GetMemoryStatus();
        var hardware = MacSystemInfo.GetHardwareInfo();
        var cpuUsage = MacSystemInfo.GetCpuUsageEstimate();
        var gateway = GetDefaultGateway()?.ToString() ?? "Not available";
        var dnsServers = GetDnsServers();

        return new Dictionary<string, object?>
        {
            ["Timestamp"] = DateTimeOffset.Now,
            ["DeviceName"] = health.DeviceName,
            ["Username"] = health.CurrentWindowsUsername,
            ["SerialNumber"] = hardware.SerialNumber,
            ["Manufacturer"] = hardware.Manufacturer,
            ["Model"] = hardware.Model,
            ["MacOSVersion"] = health.WindowsVersion,
            ["MacOSBuild"] = MacSystemInfo.GetMacOSBuild(),
            ["LastRebootTime"] = DateTimeOffset.Now - health.Uptime,
            ["Uptime"] = FormatUptime(health.Uptime),
            ["CpuName"] = MacSystemInfo.GetCpuName(),
            ["CpuUsage"] = cpuUsage.HasValue ? $"{cpuUsage.Value:0.0}%" : "Not collected yet",
            ["RamTotal"] = FormatBytes((long)memory.TotalBytes),
            ["RamAvailablePercent"] = $"{memory.AvailablePercent:0.0}%",
            ["DiskTotal"] = FormatBytes(drive.TotalSize),
            ["DiskFree"] = FormatBytes(drive.AvailableFreeSpace),
            ["DiskFreePercent"] = $"{drive.AvailableFreeSpace / (double)drive.TotalSize * 100:0.0}%",
            ["Battery"] = health.BatteryPercentage.HasValue ? $"{health.BatteryPercentage.Value}%" : "Not available",
            ["IpAddress"] = string.Join(", ", GetIPv4Addresses()),
            ["MacAddress"] = GetMacAddress(),
            ["Gateway"] = gateway,
            ["DnsServers"] = dnsServers,
            ["InternetConnectivity"] = health.InternetConnectivityStatus,
            ["FirewallStatus"] = GetFirewallStatus(),
            ["FileVaultStatus"] = GetFileVaultStatus(),
            ["GatekeeperStatus"] = GetGatekeeperStatus(),
            ["SipStatus"] = GetSipStatus(),
            ["MdmJumpCloudStatus"] = MacSystemInfo.IsAnyProcessRunning(MacApplicationProfiles.JumpCloud.ProcessNames) ? "Detected" : "Not detected",
            ["PendingUpdatesSummary"] = "Run the IT Software Update Check workflow for details",
            ["FailedServices"] = MacSystemInfo.GetFailedLaunchServices(),
            ["CrashReportsLast7Days"] = MacSystemInfo.CountRecentDiagnosticReports(7),
            ["LatestZenITActions"] = _logService.GetLatestSummaries(20),
            ["LastWorkflowResult"] = _logService.GetLatestSummaries(1).FirstOrDefault()?.Result ?? "Not available"
        };
    }

    private async Task<WorkflowExecutionResult> RunAdminCommandWorkflowAsync(WorkflowId workflowId, string successMessage, string fileName, string arguments, TimeSpan timeout, DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        if (!IsCurrentProcessElevated())
        {
            AddStep(steps, "Check administrator permissions", false, $"RequiresAdmin=True; Command={fileName} {arguments}");
            return CreateResult(workflowId, false, "This repair requires administrator permissions.", true, startedAt, steps);
        }

        await RunControlledProcessStepAsync(steps, $"Run {workflowId}", fileName, arguments, timeout, cancellationToken);
        return CreateResult(workflowId, steps.All(step => step.Success), steps.All(step => step.Success) ? successMessage : "Repair completed with warnings.", !steps.All(step => step.Success), startedAt, steps);
    }

    private static async Task RunControlledProcessStepAsync(List<WorkflowStepResult> steps, string stepName, string fileName, string arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ValidateControlledCommand(fileName, arguments);
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
            stopwatch.Stop();
            steps.Add(new WorkflowStepResult(stepName, process.ExitCode == 0, $"Command={fileName} {arguments}; ExitCode={process.ExitCode}; Output={TrimForLog(await outputTask)}; Error={TrimForLog(await errorTask)}", stopwatch.Elapsed));
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            steps.Add(new WorkflowStepResult(stepName, false, $"Command={fileName} {arguments}; Error={exception.Message}", stopwatch.Elapsed));
        }
    }

    private static void ValidateControlledCommand(string fileName, string arguments)
    {
        var command = $"{fileName} {arguments}".Trim();
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "diskutil verifyVolume /",
            "csrutil status",
            "spctl --status",
            "fdesetup status",
            "softwareupdate -l",
            "softwareupdate -i -a",
            "mdutil -E /",
            "killall coreaudiod",
            "killall -HUP mDNSResponder",
            "launchctl kickstart -k system/org.cups.cupsd",
            "dscacheutil -flushcache"
        };

        var allowedPatterns = new[]
        {
            @"^ipconfig set en\d+ DHCP$",
            @"^networksetup -setairportpower en\d+ (on|off)$"
        };

        if (allowed.Contains(command) ||
            allowedPatterns.Any(pattern => Regex.IsMatch(command, pattern)))
        {
            return;
        }

        throw new InvalidOperationException("This IT workflow command is not registered.");
    }

    private static bool IsCurrentProcessElevated()
    {
        return Environment.IsPrivilegedProcess;
    }

    private Dictionary<string, string> WriteITReport(Dictionary<string, object?> report, string prefix = "SupportPackage")
    {
        var document = new ReportDocument(
            "ZenIT Support Package",
            GetAppVersion(),
            DateTimeOffset.Now,
            report["DeviceName"]?.ToString() ?? Environment.MachineName,
            report["Username"]?.ToString() ?? Environment.UserName,
            "Non-sensitive support details for ZenHR IT troubleshooting.",
            report,
            "This report excludes personal files, browser history, cookies, saved passwords, tokens, chat messages, emails, Google Drive file names, and full installed software inventory.");

        return new ReportExporter().Export(document, prefix).ToDictionary();
    }

    private static string GetAppVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ??
               Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ??
               "1.0.0";
    }

    private static string WriteDeviceSummary(DeviceHealthInfo health)
    {
        Directory.CreateDirectory(ZenITPaths.ReportsDirectory);
        var path = Path.Combine(ZenITPaths.ReportsDirectory, $"DeviceReport-{GetReportNamePart(health.DeviceName)}-{GetReportNamePart(health.CurrentWindowsUsername)}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, BuildHealthTechnicalMessage(health), Encoding.UTF8);
        return path;
    }

    private static string BuildHealthTechnicalMessage(DeviceHealthInfo health)
    {
        return $"Device={health.DeviceName}; User={health.CurrentWindowsUsername}; macOS={health.WindowsVersion}; Uptime={FormatUptime(health.Uptime)}; Internet={health.InternetConnectivityStatus}; DiskFree={FormatBytes(health.FreeDiskSpaceBytes)}; DiskTotal={FormatBytes(health.TotalDiskSpaceBytes)}; Battery={(health.BatteryPercentage.HasValue ? $"{health.BatteryPercentage.Value}%" : "Not available")}";
    }

    private static string GetMacAddress()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .Select(networkInterface => networkInterface.GetPhysicalAddress().ToString())
            .FirstOrDefault(address => !string.IsNullOrWhiteSpace(address)) ?? "Not available";
    }

    private static int GetCameraDeviceCount()
    {
        var output = MacSystemInfo.RunCapture("/usr/sbin/system_profiler", "SPCameraDataType", timeoutMilliseconds: 15_000) ?? string.Empty;
        return Regex.Matches(output, @"Unique ID:").Count;
    }

    private static int GetAudioInputDeviceCount()
    {
        var output = MacSystemInfo.RunCapture("/usr/sbin/system_profiler", "SPAudioDataType", timeoutMilliseconds: 15_000) ?? string.Empty;
        return Regex.Matches(output, @"Input Channels:").Count;
    }

    private static int GetAudioOutputDeviceCount()
    {
        var output = MacSystemInfo.RunCapture("/usr/sbin/system_profiler", "SPAudioDataType", timeoutMilliseconds: 15_000) ?? string.Empty;
        return Regex.Matches(output, @"Output Channels:").Count;
    }

    private static string GetFirewallStatus()
    {
        var output = MacSystemInfo.RunCapture("/usr/libexec/ApplicationFirewall/socketfilterfw", "--getglobalstate");
        return string.IsNullOrWhiteSpace(output) ? "Not available" : TrimForLog(output);
    }

    private static string GetFileVaultStatus()
    {
        var output = MacSystemInfo.RunCapture("/usr/bin/fdesetup", "status");
        return string.IsNullOrWhiteSpace(output) ? "Not available" : TrimForLog(output);
    }

    private static string GetGatekeeperStatus()
    {
        var output = MacSystemInfo.RunCapture("/usr/sbin/spctl", "--status");
        return string.IsNullOrWhiteSpace(output) ? "Not available" : TrimForLog(output);
    }

    private static string GetSipStatus()
    {
        var output = MacSystemInfo.RunCapture("/usr/bin/csrutil", "status");
        return string.IsNullOrWhiteSpace(output) ? "Not available" : TrimForLog(output);
    }

    private static int CountDirectoryEntries(string path)
    {
        try
        {
            return Directory.Exists(path) ? Directory.EnumerateFileSystemEntries(path).Count() : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string GetReportNamePart(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Where(character => !invalidCharacters.Contains(character))
            .Select(character => char.IsWhiteSpace(character) ? '-' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        }

        return $"{uptime.Hours}h {uptime.Minutes}m";
    }

    private static string FormatBytes(long bytes)
    {
        const double gibibyte = 1024d * 1024d * 1024d;
        return $"{bytes / gibibyte:0.0} GB";
    }

    private static string TrimForLog(string value)
    {
        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    private sealed record NetworkAdapterSummary(string Name, string Type, string Status, bool Enabled);
}
