using System.Reflection;
using System.Text;
using ZenIT.Core.Actions;
using ZenIT.Core.Configuration;
using ZenIT.Core.Execution;
using ZenIT.Core.Logging;
using ZenIT.Core.Models;

namespace ZenIT.Core.Services;

public sealed class LocalActionExecutor : IActionExecutor
{
    private readonly ProcessRunner _processRunner;
    private readonly DeviceHealthService _deviceHealthService;
    private readonly LogService _logService;
    private readonly AppSettings _settings;

    public LocalActionExecutor(
        ProcessRunner processRunner,
        DeviceHealthService deviceHealthService,
        LogService? logService = null,
        AppSettings? settings = null)
    {
        _processRunner = processRunner;
        _deviceHealthService = deviceHealthService;
        _logService = logService ?? new LogService();
        _settings = settings ?? new AppSettings();
    }

    public async Task<ActionExecutionResult> ExecuteAsync(ActionId actionId, CancellationToken cancellationToken = default)
    {
        var definition = ActionRegistry.GetRequired(actionId);
        var startedAt = DateTimeOffset.Now;

        if (definition.RequiresAdmin)
        {
            return CreateResult(
                actionId,
                success: false,
                "This action will be available in a future managed version.",
                "Action requires administrator permissions and was not executed.",
                startedAt);
        }

        return actionId switch
        {
            ActionId.FixInternet => await FixInternetAsync(startedAt, cancellationToken),
            ActionId.FixZoom => RepairApplication(
                actionId,
                GetZoomProfile(),
                GetZoomCachePaths(),
                "Zoom refresh completed.",
                startedAt),
            ActionId.FixSlack => RepairApplication(
                actionId,
                GetSlackProfile(),
                GetSlackCachePaths(),
                "Slack refresh completed.",
                startedAt),
            ActionId.FixChrome => RepairApplication(
                actionId,
                GetChromeProfile(),
                GetChromeCachePaths(),
                "Chrome refresh completed.",
                startedAt),
            ActionId.FixGoogleDrive => FixGoogleDrive(startedAt),
            ActionId.DeviceHealthCheck => CreateDeviceReport(startedAt),
            ActionId.RequestITHelp => CreateHelpRequestPackage(startedAt),
            ActionId.FixCamera => CreateResult(
                actionId,
                success: true,
                "Please close apps using the camera, then reopen your meeting app. A managed camera repair will be available in a future version.",
                "Guided camera check returned without making system changes.",
                startedAt),
            ActionId.FixMicrophone => CreateResult(
                actionId,
                success: true,
                "Please check your selected microphone in Zoom. A managed audio repair will be available in a future version.",
                "Guided microphone check returned without making system changes.",
                startedAt),
            ActionId.RestartHelper => CreateResult(
                actionId,
                success: true,
                "Please save your work first. Then restart your device from the Start menu. Automatic restart will be added later with confirmation.",
                "Restart helper returned guidance only; no restart command executed.",
                startedAt),
            _ => throw new InvalidOperationException($"Action '{actionId}' is not registered.")
        };
    }

    private async Task<ActionExecutionResult> FixInternetAsync(DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var technicalMessages = new List<string>();
        foreach (var argument in new[] { "/flushdns", "/release", "/renew" })
        {
            var result = await _processRunner.RunAsync("ipconfig", argument, TimeSpan.FromSeconds(30), cancellationToken);
            technicalMessages.Add($"{result.FileName} {result.Arguments}: exit={result.ExitCode}; stderr={TrimForLog(result.StandardError)}");
        }

        return CreateResult(
            ActionId.FixInternet,
            success: true,
            "Network refresh completed. If the issue continues, restart your device or contact IT.",
            string.Join(" | ", technicalMessages),
            startedAt);
    }

    private static ActionExecutionResult RepairApplication(
        ActionId actionId,
        ApplicationProcessProfile profile,
        IReadOnlyCollection<string> cachePaths,
        string successMessage,
        DateTimeOffset startedAt)
    {
        var manager = new ApplicationProcessManager(profile);
        var snapshot = manager.GetSnapshot();
        var technicalMessages = new List<string> { $"Detected={snapshot.IsRunning}; {snapshot.TechnicalMessage}" };

        if (snapshot.IsRunning)
        {
            var graceful = manager.CloseGracefully(TimeSpan.FromSeconds(5));
            technicalMessages.Add($"GracefulClose: success={graceful.Success}; {graceful.TechnicalMessage}");
            var stopped = manager.VerifyStopped(TimeSpan.FromSeconds(1));
            technicalMessages.Add($"VerifyStoppedAfterGraceful: success={stopped.Success}; {stopped.TechnicalMessage}");
            if (!stopped.Success)
            {
                var forced = manager.ForceTerminate(TimeSpan.FromSeconds(5));
                technicalMessages.Add($"ForceTerminate: success={forced.Success}; {forced.TechnicalMessage}");
                stopped = manager.VerifyStopped(TimeSpan.FromSeconds(5));
                technicalMessages.Add($"VerifyStoppedAfterForce: success={stopped.Success}; {stopped.TechnicalMessage}");
                if (!stopped.Success)
                {
                    return CreateResult(actionId, false, $"{profile.DisplayName} needs IT support.", string.Join(" | ", technicalMessages), startedAt);
                }
            }
        }

        var deletedPaths = 0;
        var skippedMissingPaths = 0;
        var errors = new List<string>();

        foreach (var path in cachePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(path))
            {
                skippedMissingPaths++;
                continue;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                deletedPaths++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{path}: {exception.Message}");
            }
        }

        var success = errors.Count == 0;
        technicalMessages.Add($"DeletedPaths={deletedPaths}; SkippedMissingPaths={skippedMissingPaths}; Errors={string.Join(" | ", errors.Select(TrimForLog))}");

        if (snapshot.IsRunning)
        {
            var restart = manager.Restart();
            technicalMessages.Add($"Restart: success={restart.Success}; {restart.TechnicalMessage}");
            var verified = manager.VerifyRunning(TimeSpan.FromSeconds(10));
            technicalMessages.Add($"VerifyRestart: success={verified.Success}; {verified.TechnicalMessage}");
            success &= verified.Success;
        }

        return CreateResult(
            actionId,
            success,
            success ? successMessage : $"{profile.DisplayName} repair was attempted but ZenIT could not verify it.",
            string.Join(" | ", technicalMessages),
            startedAt);
    }

    private static ActionExecutionResult FixGoogleDrive(DateTimeOffset startedAt)
    {
        var profile = GetGoogleDriveProfile();
        var manager = new ApplicationProcessManager(profile);
        var snapshot = manager.GetSnapshot();
        var technicalMessages = new List<string> { $"Detected={snapshot.IsRunning}; {snapshot.TechnicalMessage}" };
        if (snapshot.IsRunning)
        {
            var graceful = manager.CloseGracefully(TimeSpan.FromSeconds(5));
            technicalMessages.Add($"GracefulClose: success={graceful.Success}; {graceful.TechnicalMessage}");
            if (!manager.VerifyStopped(TimeSpan.FromSeconds(1)).Success)
            {
                var forced = manager.ForceTerminate(TimeSpan.FromSeconds(5));
                technicalMessages.Add($"ForceTerminate: success={forced.Success}; {forced.TechnicalMessage}");
                if (!manager.VerifyStopped(TimeSpan.FromSeconds(5)).Success)
                {
                    return CreateResult(ActionId.FixGoogleDrive, false, "Google Drive needs IT support.", string.Join(" | ", technicalMessages), startedAt);
                }
            }

            var restart = manager.Restart(System.Diagnostics.ProcessWindowStyle.Minimized);
            technicalMessages.Add($"Restart: success={restart.Success}; {restart.TechnicalMessage}");
            var verified = manager.VerifyRunning(TimeSpan.FromSeconds(10));
            technicalMessages.Add($"VerifyRestart: success={verified.Success}; {verified.TechnicalMessage}");
            return CreateResult(ActionId.FixGoogleDrive, verified.Success, verified.Success ? "Google Drive check completed." : "Google Drive repair was attempted but ZenIT could not verify Drive restarted.", string.Join(" | ", technicalMessages), startedAt);
        }

        return CreateResult(
            ActionId.FixGoogleDrive,
            success: true,
            "Google Drive check completed.",
            string.Join(" | ", technicalMessages),
            startedAt);
    }

    private ActionExecutionResult CreateDeviceReport(DateTimeOffset startedAt)
    {
        var health = _deviceHealthService.GetCurrentHealth();
        var reportsDirectory = ZenITPaths.ReportsDirectory;
        Directory.CreateDirectory(reportsDirectory);

        var reportPath = Path.Combine(reportsDirectory, $"DeviceReport-{GetReportNamePart(health.DeviceName)}-{GetReportNamePart(health.CurrentWindowsUsername)}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(reportPath, BuildReport(health), Encoding.UTF8);

        return CreateResult(
            ActionId.DeviceHealthCheck,
            success: true,
            "Device check completed. A support report was prepared for IT.",
            $"ReportPath={reportPath}",
            startedAt,
            reportPath);
    }

    private ActionExecutionResult CreateHelpRequestPackage(DateTimeOffset startedAt)
    {
        var health = _deviceHealthService.GetCurrentHealth();
        var reportsDirectory = ZenITPaths.ReportsDirectory;
        Directory.CreateDirectory(reportsDirectory);

        var reportPath = Path.Combine(reportsDirectory, $"HelpRequest-{GetReportNamePart(health.DeviceName)}-{GetReportNamePart(health.CurrentWindowsUsername)}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(reportPath, BuildHelpRequest(health, _logService.GetLatestSummaries(20), _settings), Encoding.UTF8);

        return CreateResult(
            ActionId.RequestITHelp,
            success: true,
            "Help request prepared. Use Contact IT when you are ready.",
            $"ReportPath={reportPath}; IncludedLatestLogSummaries=20",
            startedAt,
            reportPath);
    }

    private static IReadOnlyCollection<string> GetZoomCachePaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return
        [
            Path.Combine(appData, "Zoom", "data", "Cache"),
            Path.Combine(appData, "Zoom", "data", "Code Cache"),
            Path.Combine(appData, "Zoom", "data", "GPUCache"),
            Path.Combine(appData, "Zoom", "data", "WebviewCache")
        ];
    }

    private static ApplicationProcessProfile GetZoomProfile()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new ApplicationProcessProfile(
            "Zoom",
            ["Zoom", "zoom", "Zoom Meetings", "CptHost", "zCrashReport", "aomhost", "ZoomUpdate"],
            [
                Path.Combine(appData, "Zoom", "bin", "Zoom.exe"),
                Path.Combine(localAppData, "Programs", "Zoom", "bin", "Zoom.exe")
            ]);
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

    private static ApplicationProcessProfile GetSlackProfile()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new ApplicationProcessProfile(
            "Slack",
            ["Slack", "slack"],
            [
                Path.Combine(localAppData, "slack", "slack.exe"),
                Path.Combine(localAppData, "Programs", "Slack", "slack.exe")
            ]);
    }

    private static IReadOnlyCollection<string> GetChromeCachePaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userData = Path.Combine(localAppData, "Google", "Chrome", "User Data");
        if (!Directory.Exists(userData))
        {
            return [];
        }

        var paths = new List<string>();
        foreach (var profilePath in Directory.EnumerateDirectories(userData))
        {
            paths.Add(Path.Combine(profilePath, "Cache"));
            paths.Add(Path.Combine(profilePath, "Code Cache"));
            paths.Add(Path.Combine(profilePath, "GPUCache"));
        }

        return paths;
    }

    private static ApplicationProcessProfile GetChromeProfile()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new ApplicationProcessProfile(
            "Chrome",
            ["chrome", "GoogleCrashHandler", "GoogleCrashHandler64", "GoogleUpdate", "GoogleUpdateBroker"],
            [
                Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe")
            ]);
    }

    private static ApplicationProcessProfile GetGoogleDriveProfile()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new ApplicationProcessProfile(
            "Google Drive",
            ["GoogleDriveFS", "GoogleDrive", "googledrivesync"],
            [
                Path.Combine(programFiles, "Google", "Drive File Stream", "GoogleDriveFS.exe"),
                Path.Combine(programFiles, "Google", "DriveFS", "GoogleDriveFS.exe"),
                Path.Combine(programFilesX86, "Google", "DriveFS", "GoogleDriveFS.exe")
            ]);
    }

    private static string BuildReport(DeviceHealthInfo health)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ZenIT Device Report");
        builder.AppendLine("===================");
        builder.AppendLine($"Timestamp: {health.CurrentLocalTime:O}");
        builder.AppendLine($"Device name: {health.DeviceName}");
        builder.AppendLine($"Username: {health.CurrentWindowsUsername}");
        builder.AppendLine($"Windows version: {health.WindowsVersion}");
        builder.AppendLine($"Uptime: {FormatUptime(health.Uptime)}");
        builder.AppendLine($"Internet status: {health.InternetConnectivityStatus}");
        builder.AppendLine($"Disk C free: {FormatBytes(health.FreeDiskSpaceBytes)}");
        builder.AppendLine($"Disk C total: {FormatBytes(health.TotalDiskSpaceBytes)}");
        builder.AppendLine($"Battery: {(health.BatteryPercentage.HasValue ? $"{health.BatteryPercentage.Value}%" : "Not available")}");
        builder.AppendLine($"ZenIT app version: {GetAppVersion()}");
        return builder.ToString();
    }

    private static string BuildHelpRequest(DeviceHealthInfo health, IReadOnlyList<ActionLogSummary> summaries, AppSettings settings)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ZenIT Help Request");
        builder.AppendLine("==================");
        builder.AppendLine($"Company: {settings.CompanyName}");
        builder.AppendLine($"IT support email: {settings.ITSupportEmail}");
        builder.AppendLine($"App mode: {settings.AppMode}");
        builder.AppendLine();
        builder.AppendLine("Device Health");
        builder.AppendLine("-------------");
        builder.AppendLine(BuildReport(health));
        builder.AppendLine();
        builder.AppendLine("Latest ZenIT Actions");
        builder.AppendLine("--------------------");

        if (summaries.Count == 0)
        {
            builder.AppendLine("No ZenIT action history found.");
        }
        else
        {
            foreach (var summary in summaries)
            {
                builder.AppendLine($"{summary.Timestamp:g} - {summary.ActionName} - {summary.Result}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Privacy note: This package does not include personal files, passwords, browser history, cookies, tokens, private data, or installed software lists.");
        return builder.ToString();
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

    private static bool IsAnyProcessRunning(IReadOnlyCollection<string> processNames)
    {
        return processNames.Any(processName => System.Diagnostics.Process.GetProcessesByName(processName).Length > 0);
    }

    private static ActionExecutionResult CreateResult(
        ActionId actionId,
        bool success,
        string userMessage,
        string technicalMessage,
        DateTimeOffset startedAt,
        string? reportPath = null)
    {
        return new ActionExecutionResult(
            actionId,
            success,
            userMessage,
            technicalMessage,
            startedAt,
            DateTimeOffset.Now,
            reportPath);
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

    private static string GetAppVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Not available";
    }

    private static string TrimForLog(string value)
    {
        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }
}
