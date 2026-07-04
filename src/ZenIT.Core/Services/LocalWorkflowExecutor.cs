using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using ZenIT.Core.Configuration;
using ZenIT.Core.Execution;
using ZenIT.Core.Logging;
using ZenIT.Core.Models;
using ZenIT.Core.Reports;
using ZenIT.Core.Workflows;

namespace ZenIT.Core.Services;

[SupportedOSPlatform("windows")]
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
            WorkflowId.FullWindowsRepairCheck => FullWindowsRepairCheck(startedAt, steps),
            WorkflowId.ItFlushDns => await ItFlushDnsAsync(startedAt, steps, cancellationToken),
            WorkflowId.ItReleaseRenewIp => await ItReleaseRenewIpAsync(startedAt, steps, cancellationToken),
            WorkflowId.RestartNetworkAdapter => RestartNetworkAdapter(startedAt, steps),
            WorkflowId.DnsRepair => DnsRepair(startedAt, steps),
            WorkflowId.SfcScan => await RunAdminCommandWorkflowAsync(WorkflowId.SfcScan, "SFC Scan completed.", "sfc", "/scannow", TimeSpan.FromMinutes(30), startedAt, steps, cancellationToken),
            WorkflowId.DismScanHealth => await RunAdminCommandWorkflowAsync(WorkflowId.DismScanHealth, "DISM ScanHealth completed.", "DISM", "/Online /Cleanup-Image /ScanHealth", TimeSpan.FromMinutes(30), startedAt, steps, cancellationToken),
            WorkflowId.DismHealthRestore => await RunAdminCommandWorkflowAsync(WorkflowId.DismHealthRestore, "DISM health restore completed.", "DISM", "/Online /Cleanup-Image /RestoreHealth", TimeSpan.FromMinutes(60), startedAt, steps, cancellationToken),
            WorkflowId.WindowsUpdateRepair => WindowsUpdateRepair(startedAt, steps),
            WorkflowId.ItTempCleanup => ItTempCleanup(startedAt, steps),
            WorkflowId.StartupAnalysis => StartupAnalysis(startedAt, steps),
            WorkflowId.RestartPrintSpooler => await RestartServiceWorkflowAsync(WorkflowId.RestartPrintSpooler, "Print Spooler", "spooler", startedAt, steps, cancellationToken),
            WorkflowId.RestartAudioServices => await RestartAudioServicesAsync(startedAt, steps, cancellationToken),
            WorkflowId.RestartWindowsUpdate => await RestartServiceWorkflowAsync(WorkflowId.RestartWindowsUpdate, "Windows Update", "wuauserv", startedAt, steps, cancellationToken),
            WorkflowId.RestartBits => await RestartServiceWorkflowAsync(WorkflowId.RestartBits, "BITS", "bits", startedAt, steps, cancellationToken),
            WorkflowId.NetworkStackReset => await RunAdminCommandWorkflowAsync(WorkflowId.NetworkStackReset, "Network stack reset completed. Please restart the device.", "netsh", "int ip reset", TimeSpan.FromMinutes(2), startedAt, steps, cancellationToken),
            WorkflowId.WinsockReset => await RunAdminCommandWorkflowAsync(WorkflowId.WinsockReset, "Winsock reset completed. Please restart the device.", "netsh", "winsock reset", TimeSpan.FromMinutes(2), startedAt, steps, cancellationToken),
            WorkflowId.WingetUpgradeAll => await WingetUpgradeAllAsync(startedAt, steps, cancellationToken),
            WorkflowId.AdvancedEventReport => AdvancedEventReport(startedAt, steps),
            WorkflowId.ServiceHealthRepair => ServiceHealthRepair(startedAt, steps),
            WorkflowId.ExportAdvancedDiagnosticPackage => ExportAdvancedDiagnosticPackage(startedAt, steps),
            _ => throw new InvalidOperationException($"Workflow '{workflowId}' is not registered.")
        };
    }

    /// <summary>
    /// Diagnoses network state before repair, runs only the safe command needed, and verifies gateway, DNS, and internet access.
    /// Employee Mode never performs Winsock/TCP resets or adapter toggles because those require approved elevated tooling.
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
            await RunProcessStepAsync(steps, "Repair IP configuration - release", "ipconfig", "/release", cancellationToken);
            await RunProcessStepAsync(steps, "Repair IP configuration - renew", "ipconfig", "/renew", cancellationToken);
            hasIp = RunStep(steps, "Verify IP configuration", () =>
            {
                var ips = GetIPv4Addresses();
                return (ips.Count > 0, $"IPv4={string.Join(",", ips)}");
            });
            gatewayOk = await VerifyGatewayAsync(steps, "Verify gateway after renew", cancellationToken);
        }

        if (!hasDnsServers || !dnsOk)
        {
            await RunProcessStepAsync(steps, "Repair DNS - flush cache", "ipconfig", "/flushdns", cancellationToken);
            if (IsCurrentProcessElevated())
            {
                await RunProcessStepAsync(steps, "Repair DNS - register", "ipconfig", "/registerdns", cancellationToken);
            }
            else
            {
                AddStep(steps, "Repair DNS - register", false, "RequiresAdmin=True; Skipped=True");
            }

            dnsOk = VerifyDns(steps, "Verify DNS after refresh");
        }

        internetOk = await VerifyInternetAsync(steps, "Verify internet after repair", cancellationToken);

        if (connectedNetwork && hasIp && gatewayOk && internetOk && dnsOk)
        {
            return CreateResult(WorkflowId.InternetNotWorking, true, "Internet connection restored.", false, startedAt, steps);
        }

        if (!dnsOk)
        {
            AddStep(steps, "Repair network stack", false, "RequiresITMode=True; WinsockTcpResetSkipped=True; EmployeeModeDoesNotRunNetshReset=True");
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
    /// Checks CPU/RAM/disk/uptime/reboot state and clears safe temporary/cache locations only.
    /// It does not touch Downloads, Documents, Desktop, browser passwords/history, startup apps, or unrelated processes.
    /// </summary>
    private WorkflowExecutionResult ImproveDevicePerformance(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var driveBefore = GetSystemDriveFreeBytes();
        RunStep(steps, "Check CPU usage", () => (true, $"ProcessCount={Process.GetProcesses().Length}"));
        var memoryLow = false;
        RunStep(steps, "Check RAM usage", () =>
        {
            var memory = GetMemoryStatus();
            memoryLow = memory.AvailablePercent < 15;
            return (!memoryLow, $"AvailableMemoryPercent={memory.AvailablePercent:0.0}; TotalMemory={FormatBytes((long)memory.TotalBytes)}");
        });

        var diskLow = false;
        RunStep(steps, "Check disk free space", () =>
        {
            var drive = new DriveInfo(@"C:\");
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
        RunStep(steps, "Detect pending reboot", () => (true, $"PendingReboot={IsPendingReboot()}"));
        ClearTempFolder(steps, Path.GetTempPath());
        ClearTempFolder(steps, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"));
        ClearWindowsTempFolder(steps);
        ClearCachePaths(steps, "DeliveryOptimizationCache", [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\DeliveryOptimization\Cache")]);
        ClearCachePaths(steps, "WindowsErrorReportingTemp", [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"CrashDumps"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\WER\Temp")
        ]);
        AddStep(steps, "Recycle Bin cleanup", true, "Skipped=True; EmployeeModeDoesNotEmptyRecycleBin=True");
        RefreshExplorerView(steps);

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
        await RunProcessStepAsync(steps, "Flush DNS", "ipconfig", "/flushdns", cancellationToken);
        RunStep(steps, "Check updates status", () => (true, $"PendingReboot={IsPendingReboot()}"));
        RunStep(steps, "Check disk space", () =>
        {
            var drive = new DriveInfo(@"C:\");
            return (drive.AvailableFreeSpace > 10L * 1024L * 1024L * 1024L, $"Free={FormatBytes(drive.AvailableFreeSpace)}; Total={FormatBytes(drive.TotalSize)}");
        });
        AppendNestedResult(steps, ImproveDevicePerformance(startedAt, []));
        AppendNestedResult(steps, ChromeNotWorkingForFixEverything(startedAt, []));
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
    /// Performs meeting-device diagnostics only: privacy state, best-effort device registry hints, and meeting app process state.
    /// It does not modify registry, restart services, force-close apps, or change default devices.
    /// </summary>
    private WorkflowExecutionResult MeetingDevices(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var cameraPermissionOk = RunStep(steps, "Check camera privacy permission", () => ReadConsentStore("webcam"));
        var microphonePermissionOk = RunStep(steps, "Check microphone privacy permission", () => ReadConsentStore("microphone"));
        var cameraDeviceHint = RunStep(steps, "Check camera device hints", () =>
        {
            var count = GetCameraDeviceHintCount();
            return (count > 0, $"CameraDeviceRegistryHints={count}");
        });
        var microphoneDeviceHint = RunStep(steps, "Check microphone device hints", () =>
        {
            var count = GetAudioCaptureDeviceHintCount();
            return (count > 0, $"AudioCaptureDeviceRegistryHints={count}");
        });
        RunStep(steps, "Check Zoom process state", () => (true, $"ZoomRunning={IsAnyProcessRunning(["Zoom"])}"));
        RunStep(steps, "Check Chrome process state", () => (true, $"ChromeRunning={IsAnyProcessRunning(["chrome"])}"));
        var needsSupport = !cameraPermissionOk || !microphonePermissionOk || !cameraDeviceHint || !microphoneDeviceHint;

        return CreateResult(
            WorkflowId.CameraOrMicrophoneNotWorking,
            !needsSupport,
            needsSupport ? "Meeting device issue detected. Please contact IT." : "Meeting device check completed.",
            needsSupport,
            startedAt,
            steps);
    }

    /// <summary>
    /// Performs audio diagnostics only: Windows audio engine hint, endpoint registry hints, and capture/render device hints.
    /// It does not restart services or change default speaker/microphone settings.
    /// </summary>
    private WorkflowExecutionResult SoundNotWorking(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var needsSupport = false;
        RunStep(steps, "Check Windows Audio service status", () =>
        {
            var running = Process.GetProcessesByName("audiodg").Length > 0;
            needsSupport = !running;
            return (running, $"AudioEngineRunning={running}");
        });
        RunStep(steps, "Check audio endpoint status", () =>
        {
            var renderCount = GetAudioRenderDeviceHintCount();
            needsSupport |= renderCount == 0;
            return (renderCount > 0, $"AudioRenderDeviceRegistryHints={renderCount}");
        });
        RunStep(steps, "Check audio devices", () =>
        {
            var captureCount = GetAudioCaptureDeviceHintCount();
            return (captureCount > 0, $"AudioCaptureDeviceRegistryHints={captureCount}");
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
            GetChromeProfile(),
            GetChromeCachePaths(),
            "Chrome refresh completed.",
            "Chrome repair was attempted but ZenIT could not verify Chrome restarted.",
            startedAt,
            steps,
            afterRepair: () => RunStep(steps, "Check browser network availability", () => (NetworkInterface.GetIsNetworkAvailable(), "NetworkAvailable")));
    }

    private WorkflowExecutionResult ChromeNotWorkingForFixEverything(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        return ChromeNotWorking(startedAt, steps);
    }

    /// <summary>
    /// Refreshes Slack automatically using only Slack's allowlisted processes and safe current-user cache folders.
    /// It does not delete downloads, workspace credentials, or user files.
    /// </summary>
    private WorkflowExecutionResult SlackNotWorking(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        return RepairApplicationWorkflow(
            WorkflowId.SlackNotWorking,
            GetSlackProfile(),
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
            GetZoomProfile(),
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
        var profile = GetGoogleDriveProfile();
        var manager = new ApplicationProcessManager(profile);
        var snapshot = manager.GetSnapshot();
        AddStep(steps, "Diagnose Google Drive process state", true, snapshot.TechnicalMessage);
        RunStep(steps, "Check internet connectivity", () => (NetworkInterface.GetIsNetworkAvailable(), "NetworkAvailable"));
        RunStep(steps, "Check disk free space", () =>
        {
            var drive = new DriveInfo(@"C:\");
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
        var running = new ApplicationProcessManager(GetGoogleDriveProfile()).IsRunning();
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
    /// Performs read-only endpoint security checks: Kaspersky process hints, firewall registry state, BitLocker placeholder,
    /// JumpCloud process hints, and Defender placeholder. It never changes security settings.
    /// </summary>
    private WorkflowExecutionResult SecurityCheck(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var needsSupport = false;
        RunStep(steps, "Check Kaspersky status", () =>
        {
            var running = IsAnyProcessRunning(["avp", "kavfs", "klnagent"]);
            needsSupport |= !running;
            return (running, $"KasperskyProcessDetected={running}");
        });
        RunStep(steps, "Check Windows Firewall status", () =>
        {
            var status = GetWindowsFirewallStatus();
            needsSupport |= status.Contains("Disabled", StringComparison.OrdinalIgnoreCase);
            return (!status.Contains("Disabled", StringComparison.OrdinalIgnoreCase), $"FirewallStatus={status}");
        });
        RunStep(steps, "Check BitLocker status", () => (true, "BitLockerStatus=ReadOnlyCheckRequiresManagedService"));
        RunStep(steps, "Check MDM state", () =>
        {
            var jumpCloudDetected = IsAnyProcessRunning(["jumpcloud-agent", "jcagent", "JumpCloud"]);
            needsSupport |= !jumpCloudDetected;
            return (jumpCloudDetected, $"JumpCloudProcessDetected={jumpCloudDetected}");
        });
        RunStep(steps, "Check Windows Defender status", () => (true, "DefenderStatus=ReadOnlyCheckRequiresManagedService"));

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
    /// Windows health, and ZenIT activity summaries. It excludes personal files, browser data, cookies, passwords, tokens,
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
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            var enabled = key?.GetValue("ProxyEnable")?.ToString() ?? "Unknown";
            return (true, $"ProxyEnable={enabled}");
        });

        RunStep(steps, "Diagnose VPN state", () =>
        {
            var vpnProcesses = Process.GetProcesses().Count(process => process.ProcessName.Contains("vpn", StringComparison.OrdinalIgnoreCase));
            return (true, $"VpnLikeProcesses={vpnProcesses}");
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

    private static (bool Success, string TechnicalMessage) ReadConsentStore(string capability)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{capability}");
        var value = key?.GetValue("Value")?.ToString() ?? "Unknown";
        return (value != "Deny", $"{capability}Permission={value}");
    }

    private static void ClearTempFolder(List<WorkflowStepResult> steps, string folderPath)
    {
        ClearCachePaths(steps, $"Temp:{folderPath}", [folderPath]);
    }

    private static void ClearWindowsTempFolder(List<WorkflowStepResult> steps)
    {
        ClearCachePaths(steps, "WindowsTempAccessibleFiles", [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")]);
    }

    private static long GetSystemDriveFreeBytes()
    {
        return new DriveInfo(@"C:\").AvailableFreeSpace;
    }

    private static void RefreshExplorerView(List<WorkflowStepResult> steps)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            stopwatch.Stop();
            steps.Add(new WorkflowStepResult("Refresh Explorer", true, "ShellChangeNotify=Sent", stopwatch.Elapsed));
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            steps.Add(new WorkflowStepResult("Refresh Explorer", false, exception.Message, stopwatch.Elapsed));
        }
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

    private static IReadOnlyCollection<string> GetSlackCachePaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return
        [
            Path.Combine(appData, "Slack", "Cache"),
            Path.Combine(appData, "Slack", "Code Cache"),
            Path.Combine(appData, "Slack", "GPUCache"),
            Path.Combine(appData, "Slack", "Service Worker", "CacheStorage")
        ];
    }

    private static IReadOnlyCollection<string> GetChromeCachePaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var chromeDefault = Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default");
        return
        [
            Path.Combine(chromeDefault, "Cache"),
            Path.Combine(chromeDefault, "Code Cache"),
            Path.Combine(chromeDefault, "GPUCache"),
            Path.Combine(chromeDefault, "Service Worker", "CacheStorage")
        ];
    }

    private static IReadOnlyCollection<string> GetZoomCachePaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return
        [
            Path.Combine(appData, "Zoom", "data"),
            Path.Combine(appData, "Zoom", "logs"),
            Path.Combine(localAppData, "Zoom", "cache")
        ];
    }

    private static IReadOnlyCollection<string> GetSlackExecutableCandidates()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return
        [
            Path.Combine(localAppData, "slack", "slack.exe"),
            Path.Combine(localAppData, "Programs", "Slack", "slack.exe")
        ];
    }

    private static ApplicationProcessProfile GetSlackProfile()
    {
        return new ApplicationProcessProfile(
            "Slack",
            ["Slack", "slack"],
            GetSlackExecutableCandidates());
    }

    private static IReadOnlyCollection<string> GetChromeExecutableCandidates()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return
        [
            Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe")
        ];
    }

    private static ApplicationProcessProfile GetChromeProfile()
    {
        return new ApplicationProcessProfile(
            "Chrome",
            ["chrome", "GoogleCrashHandler", "GoogleCrashHandler64", "GoogleUpdate", "GoogleUpdateBroker"],
            GetChromeExecutableCandidates());
    }

    private static IReadOnlyCollection<string> GetZoomExecutableCandidates()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return
        [
            Path.Combine(appData, "Zoom", "bin", "Zoom.exe"),
            Path.Combine(localAppData, "Programs", "Zoom", "bin", "Zoom.exe")
        ];
    }

    private static ApplicationProcessProfile GetZoomProfile()
    {
        return new ApplicationProcessProfile(
            "Zoom",
            ["Zoom", "zoom", "Zoom Meetings", "CptHost", "zCrashReport", "aomhost", "ZoomUpdate"],
            GetZoomExecutableCandidates());
    }

    private static IReadOnlyCollection<string> GetGoogleDriveExecutableCandidates()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return
        [
            Path.Combine(programFiles, "Google", "Drive File Stream", "GoogleDriveFS.exe"),
            Path.Combine(programFiles, "Google", "DriveFS", "GoogleDriveFS.exe"),
            Path.Combine(programFilesX86, "Google", "DriveFS", "GoogleDriveFS.exe")
        ];
    }

    private static ApplicationProcessProfile GetGoogleDriveProfile()
    {
        return new ApplicationProcessProfile(
            "Google Drive",
            ["GoogleDriveFS", "GoogleDrive", "googledrivesync"],
            GetGoogleDriveExecutableCandidates());
    }

    private static bool IsAnyProcessRunning(IReadOnlyCollection<string> processNames)
    {
        return processNames.Any(processName => Process.GetProcessesByName(processName).Length > 0);
    }

    private static int CountProcesses(IReadOnlyCollection<string> processNames)
    {
        return processNames.Sum(processName => Process.GetProcessesByName(processName).Length);
    }

    private static IReadOnlyList<string> GetIPv4Addresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(address => address.Address.ToString())
            .ToList();
    }

    private static IReadOnlyList<NetworkAdapterSummary> GetPhysicalNetworkAdapterSummaries()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet &&
                !networkInterface.Description.Contains("virtual", StringComparison.OrdinalIgnoreCase) &&
                !networkInterface.Description.Contains("loopback", StringComparison.OrdinalIgnoreCase))
            .Select(networkInterface => new NetworkAdapterSummary(
                networkInterface.Name,
                networkInterface.NetworkInterfaceType.ToString(),
                networkInterface.OperationalStatus.ToString(),
                networkInterface.OperationalStatus == OperationalStatus.Up))
            .ToList();
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

    private static bool IsPendingReboot()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
        return key is not null;
    }

    private static (double TotalBytes, double AvailableBytes, double AvailablePercent) GetMemoryStatus()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status))
        {
            return (0, 0, 0);
        }

        var total = (double)status.TotalPhys;
        var available = (double)status.AvailPhys;
        return (total, available, total <= 0 ? 0 : available / total * 100);
    }

    private Dictionary<string, object?> BuildITReportData()
    {
        var health = _deviceHealthService.GetCurrentHealth();
        var drive = new DriveInfo(@"C:\");
        var memory = GetMemoryStatus();
        var systemInfo = GetSystemFirmwareInfo();
        var gateway = GetDefaultGateway()?.ToString() ?? "Not available";
        var dnsServers = NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().DnsAddresses)
            .Where(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(address => address.ToString())
            .Distinct()
            .ToList();

        return new Dictionary<string, object?>
        {
            ["Timestamp"] = DateTimeOffset.Now,
            ["DeviceName"] = health.DeviceName,
            ["Username"] = health.CurrentWindowsUsername,
            ["SerialNumber"] = systemInfo.SerialNumber,
            ["Manufacturer"] = systemInfo.Manufacturer,
            ["Model"] = systemInfo.Model,
            ["WindowsVersion"] = health.WindowsVersion,
            ["WindowsBuildNumber"] = Environment.OSVersion.Version.Build,
            ["LastRebootTime"] = DateTimeOffset.Now - health.Uptime,
            ["Uptime"] = FormatUptime(health.Uptime),
            ["CpuName"] = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Not available",
            ["CpuUsage"] = "Not collected yet",
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
            ["KasperskyStatus"] = IsAnyProcessRunning(["avp", "kavfs", "klnagent"]) ? "Detected" : "Not detected",
            ["WindowsFirewallStatus"] = GetWindowsFirewallStatus(),
            ["BitLockerStatus"] = "Read-only check requires managed service",
            ["MdmJumpCloudStatus"] = IsAnyProcessRunning(["jumpcloud-agent", "jcagent", "JumpCloud"]) ? "Detected" : "Not detected",
            ["PendingRebootStatus"] = IsPendingReboot(),
            ["PendingUpdatesSummary"] = "Read-only update summary requires managed service",
            ["FailedServices"] = GetFailedServiceNames(),
            ["CriticalEventsLast7Days"] = GetCriticalEvents(),
            ["LatestZenITActions"] = _logService.GetLatestSummaries(20),
            ["LastWorkflowResult"] = _logService.GetLatestSummaries(1).FirstOrDefault()?.Result ?? "Not available"
        };
    }

    private WorkflowExecutionResult FullWindowsRepairCheck(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        RunStep(steps, "Check pending reboot", () => (true, $"PendingReboot={IsPendingReboot()}"));
        RunStep(steps, "Check component store health availability", () => (IsCurrentProcessElevated(), "DISMCheckHealthAvailableWhenElevated"));
        RunStep(steps, "Check system file scan availability", () => (IsCurrentProcessElevated(), "SFCScanAvailableWhenElevated"));
        RunStep(steps, "Check failed services", () => (true, string.Join(",", GetFailedServiceNames())));
        RunStep(steps, "Check critical events", () => (true, string.Join(",", GetCriticalEvents())));
        return CreateResult(WorkflowId.FullWindowsRepairCheck, true, "Windows repair check completed.", false, startedAt, steps);
    }

    private WorkflowExecutionResult WindowsUpdateRepair(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        RunStep(steps, "Check Windows Update repair availability", () => (IsCurrentProcessElevated(), "CacheRepairRequiresAdminAndFutureConfirmation"));
        RunStep(steps, "Create Windows Update diagnostic summary", () => (true, "PendingUpdatesSummary=ManagedServiceRequired"));
        return CreateResult(WorkflowId.WindowsUpdateRepair, true, "Windows Update diagnostic report created.", false, startedAt, steps);
    }

    private WorkflowExecutionResult ItTempCleanup(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        AppendNestedResult(steps, ImproveDevicePerformance(startedAt, []));
        return CreateResult(WorkflowId.ItTempCleanup, steps.All(step => step.Success), "Temp cleanup completed.", steps.Any(step => !step.Success), startedAt, steps);
    }

    private WorkflowExecutionResult StartupAnalysis(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        RunStep(steps, "Count startup registry entries", () =>
        {
            var currentUser = CountRegistryValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
            var localMachine = CountRegistryValues(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
            return (true, $"CurrentUserRun={currentUser}; LocalMachineRun={localMachine}; NoChangesMade=True");
        });
        RunStep(steps, "Check startup load indicator", () => (true, $"RunningProcesses={Process.GetProcesses().Length}"));
        return CreateResult(WorkflowId.StartupAnalysis, true, "Startup analysis completed.", false, startedAt, steps);
    }

    private async Task<WorkflowExecutionResult> ItFlushDnsAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        await RunProcessStepAsync(steps, "Flush DNS", "ipconfig", "/flushdns", cancellationToken);
        return CreateResult(WorkflowId.ItFlushDns, steps.All(step => step.Success), "DNS cache flushed.", !steps.All(step => step.Success), startedAt, steps);
    }

    private async Task<WorkflowExecutionResult> ItReleaseRenewIpAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        await RunProcessStepAsync(steps, "Release IP", "ipconfig", "/release", cancellationToken);
        await RunProcessStepAsync(steps, "Renew IP", "ipconfig", "/renew", cancellationToken);
        return CreateResult(WorkflowId.ItReleaseRenewIp, steps.All(step => step.Success), "IP address refreshed.", !steps.All(step => step.Success), startedAt, steps);
    }

    private WorkflowExecutionResult RestartNetworkAdapter(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        AddStep(steps, "Check administrator permissions", IsCurrentProcessElevated(), "Restarting adapters requires an elevated managed service.");
        return CreateResult(WorkflowId.RestartNetworkAdapter, false, "This repair requires administrator permissions.", true, startedAt, steps);
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

    private async Task<WorkflowExecutionResult> RestartAudioServicesAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        if (!IsCurrentProcessElevated())
        {
            AddStep(steps, "Check administrator permissions", false, "RequiresAdmin=True");
            return CreateResult(WorkflowId.RestartAudioServices, false, "This repair requires administrator permissions.", true, startedAt, steps);
        }

        await RunControlledProcessStepAsync(steps, "Restart Audio Endpoint Builder", "net", "stop AudioEndpointBuilder /y", TimeSpan.FromMinutes(1), cancellationToken);
        await RunControlledProcessStepAsync(steps, "Start Audio Endpoint Builder", "net", "start AudioEndpointBuilder", TimeSpan.FromMinutes(1), cancellationToken);
        await RunControlledProcessStepAsync(steps, "Restart Windows Audio", "net", "stop Audiosrv /y", TimeSpan.FromMinutes(1), cancellationToken);
        await RunControlledProcessStepAsync(steps, "Start Windows Audio", "net", "start Audiosrv", TimeSpan.FromMinutes(1), cancellationToken);
        return CreateResult(WorkflowId.RestartAudioServices, steps.All(step => step.Success), "Audio services restart completed.", false, startedAt, steps);
    }

    private async Task<WorkflowExecutionResult> RestartServiceWorkflowAsync(WorkflowId workflowId, string serviceDisplayName, string serviceName, DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        if (!IsCurrentProcessElevated())
        {
            AddStep(steps, "Check administrator permissions", false, "RequiresAdmin=True");
            return CreateResult(workflowId, false, "This repair requires administrator permissions.", true, startedAt, steps);
        }

        await RunControlledProcessStepAsync(steps, $"Stop {serviceDisplayName}", "net", $"stop {serviceName}", TimeSpan.FromMinutes(1), cancellationToken);
        await RunControlledProcessStepAsync(steps, $"Start {serviceDisplayName}", "net", $"start {serviceName}", TimeSpan.FromMinutes(1), cancellationToken);
        return CreateResult(workflowId, steps.All(step => step.Success), $"{serviceDisplayName} restart completed.", false, startedAt, steps);
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

    private async Task<WorkflowExecutionResult> WingetUpgradeAllAsync(DateTimeOffset startedAt, List<WorkflowStepResult> steps, CancellationToken cancellationToken)
    {
        await RunControlledProcessStepAsync(steps, "Check winget upgrades", "winget", "upgrade", TimeSpan.FromMinutes(3), cancellationToken);
        await RunControlledProcessStepAsync(steps, "Run winget upgrades", "winget", "upgrade --all --accept-package-agreements --accept-source-agreements", TimeSpan.FromMinutes(60), cancellationToken);
        return CreateResult(WorkflowId.WingetUpgradeAll, steps.All(step => step.Success), "Winget upgrade check completed.", false, startedAt, steps);
    }

    private WorkflowExecutionResult AdvancedEventReport(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        var report = BuildITReportData();
        report["AdvancedEvents"] = GetCriticalEvents();
        var paths = WriteITReport(report, "AdvancedEventReport");
        AddStep(steps, "Export advanced event report", true, $"ReportPaths={string.Join(",", paths.Values)}");
        return CreateResult(WorkflowId.AdvancedEventReport, true, "Advanced event report exported.", false, startedAt, steps, paths["txt"]);
    }

    private WorkflowExecutionResult ServiceHealthRepair(DateTimeOffset startedAt, List<WorkflowStepResult> steps)
    {
        AddStep(steps, "List failed or stopped automatic services", true, string.Join(",", GetFailedServiceNames()));
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
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sfc /scannow",
            "DISM /Online /Cleanup-Image /ScanHealth",
            "DISM /Online /Cleanup-Image /RestoreHealth",
            "net stop spooler",
            "net start spooler",
            "net stop wuauserv",
            "net start wuauserv",
            "net stop bits",
            "net start bits",
            "net stop AudioEndpointBuilder /y",
            "net start AudioEndpointBuilder",
            "net stop Audiosrv /y",
            "net start Audiosrv",
            "netsh int ip reset",
            "netsh winsock reset",
            "winget upgrade",
            "winget upgrade --all --accept-package-agreements --accept-source-agreements"
        };

        if (!allowed.Contains(command))
        {
            throw new InvalidOperationException("This IT workflow command is not registered.");
        }
    }

    private static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
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

    private string WriteHelpRequest(Dictionary<string, object?> report, Dictionary<string, string> reportPaths)
    {
        Directory.CreateDirectory(ZenITPaths.ReportsDirectory);
        var device = GetReportNamePart(report["DeviceName"]?.ToString() ?? Environment.MachineName);
        var user = GetReportNamePart(report["Username"]?.ToString() ?? Environment.UserName);
        var path = Path.Combine(ZenITPaths.ReportsDirectory, $"HelpRequest-{device}-{user}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");
        var builder = new StringBuilder();
        builder.AppendLine("ZenIT Help Request");
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        builder.AppendLine($"User: {report["Username"]}");
        builder.AppendLine($"Device: {report["DeviceName"]}");
        builder.AppendLine("Latest action result: Help request prepared.");
        builder.AppendLine("Report file paths:");
        foreach (var item in reportPaths)
        {
            builder.AppendLine($"- {item.Value}");
        }

        builder.AppendLine($"IT support email: {_settings.ITSupportEmail}");
        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        return path;
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
        return $"Device={health.DeviceName}; User={health.CurrentWindowsUsername}; Windows={health.WindowsVersion}; Uptime={FormatUptime(health.Uptime)}; Internet={health.InternetConnectivityStatus}; DiskFree={FormatBytes(health.FreeDiskSpaceBytes)}; DiskTotal={FormatBytes(health.TotalDiskSpaceBytes)}; Battery={(health.BatteryPercentage.HasValue ? $"{health.BatteryPercentage.Value}%" : "Not available")}";
    }

    private static string GetMacAddress()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .Select(networkInterface => networkInterface.GetPhysicalAddress().ToString())
            .FirstOrDefault(address => !string.IsNullOrWhiteSpace(address)) ?? "Not available";
    }

    private static int GetCameraDeviceHintCount()
    {
        return CountSubkeys(@"SYSTEM\CurrentControlSet\Control\Class\{ca3e7ab9-b4c3-4ae6-8251-579ef933890f}") +
               CountSubkeys(@"SYSTEM\CurrentControlSet\Control\Class\{6bdd1fc6-810f-11d0-bec7-08002be2092f}");
    }

    private static int GetAudioCaptureDeviceHintCount()
    {
        return CountSubkeys(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture");
    }

    private static int GetAudioRenderDeviceHintCount()
    {
        return CountSubkeys(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render");
    }

    private static int CountSubkeys(string subkeyPath)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subkeyPath);
            return key?.GetSubKeyNames().Length ?? 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return 0;
        }
    }

    private static int CountRegistryValues(RegistryKey root, string subkeyPath)
    {
        try
        {
            using var key = root.OpenSubKey(subkeyPath);
            return key?.ValueCount ?? 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return 0;
        }
    }

    private static string? GetDefaultPrinterName()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Windows");
        var deviceValue = key?.GetValue("Device")?.ToString();
        if (string.IsNullOrWhiteSpace(deviceValue))
        {
            return null;
        }

        return deviceValue.Split(',')[0].Trim();
    }

    private static string GetWindowsFirewallStatus()
    {
        var profiles = new Dictionary<string, string>
        {
            ["Domain"] = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile",
            ["Private"] = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile",
            ["Public"] = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile"
        };

        var statuses = new List<string>();
        foreach (var profile in profiles)
        {
            using var key = Registry.LocalMachine.OpenSubKey(profile.Value);
            var value = key?.GetValue("EnableFirewall");
            var enabled = value is int intValue ? intValue == 1 : value?.ToString() == "1";
            statuses.Add($"{profile.Key}={(enabled ? "Enabled" : "DisabledOrUnknown")}");
        }

        return string.Join("; ", statuses);
    }

    private static (string SerialNumber, string Manufacturer, string Model) GetSystemFirmwareInfo()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
        var serial = key?.GetValue("SystemSerialNumber")?.ToString();
        var manufacturer = key?.GetValue("SystemManufacturer")?.ToString();
        var model = key?.GetValue("SystemProductName")?.ToString();
        return (
            string.IsNullOrWhiteSpace(serial) ? "Not available" : serial,
            string.IsNullOrWhiteSpace(manufacturer) ? "Not available" : manufacturer,
            string.IsNullOrWhiteSpace(model) ? "Not available" : model);
    }

    private static IReadOnlyList<string> GetFailedServiceNames()
    {
        return ["Detailed failed service inventory requires managed service"];
    }

    private static IReadOnlyList<string> GetCriticalEvents()
    {
        return ["Critical event summary requires managed service"];
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

    private static string FormatReportDuration(TimeSpan duration)
    {
        return duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds:0.0} seconds"
            : $"{duration.TotalMinutes:0.0} minutes";
    }

    private static string TrimForLog(string value)
    {
        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

    [DllImport("Shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private sealed record NetworkAdapterSummary(string Name, string Type, string Status, bool Enabled);

    [StructLayout(LayoutKind.Sequential)]
    private sealed class MemoryStatusEx
    {
        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        }

        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
