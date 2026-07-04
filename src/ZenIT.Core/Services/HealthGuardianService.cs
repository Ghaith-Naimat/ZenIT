using System.Diagnostics;
using ZenIT.Core.Logging;

namespace ZenIT.Core.Services;

public sealed class HealthGuardianService : IDisposable
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(60);
    private readonly LogService _logService;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly IReadOnlyList<GuardianTarget> _targets;
    private Task? _monitorTask;

    public HealthGuardianService(LogService logService)
    {
        _logService = logService;
        _targets =
        [
            new(new ApplicationProcessProfile("Chrome", ["chrome", "GoogleCrashHandler", "GoogleCrashHandler64", "GoogleUpdate", "GoogleUpdateBroker"], GetChromeCandidates())),
            new(new ApplicationProcessProfile("Slack", ["Slack", "slack"], GetSlackCandidates())),
            new(new ApplicationProcessProfile("Zoom", ["Zoom", "zoom", "Zoom Meetings", "CptHost", "zCrashReport", "aomhost", "ZoomUpdate"], GetZoomCandidates())),
            new(new ApplicationProcessProfile("Google Drive", ["GoogleDriveFS", "GoogleDrive", "googledrivesync"], GetGoogleDriveCandidates())),
            new(new ApplicationProcessProfile("JumpCloud", ["jumpcloud-agent", "jcagent", "JumpCloud"], [])),
            new(new ApplicationProcessProfile("Kaspersky", ["avp", "kavfs", "klnagent"], []))
        ];
    }

    public void Start()
    {
        if (_monitorTask is not null)
        {
            return;
        }

        _monitorTask = Task.Run(() => MonitorAsync(_cancellation.Token));
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        await LogGuardianAsync("Started", "Health Guardian started.", TimeSpan.Zero, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(MonitorInterval, cancellationToken);
                foreach (var target in _targets)
                {
                    await CheckTargetAsync(target, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                await LogGuardianAsync("Error", $"Health Guardian monitor error: {exception.Message}", TimeSpan.Zero, CancellationToken.None);
            }
        }
    }

    private async Task CheckTargetAsync(GuardianTarget target, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var running = target.IsRunning();
        if (running)
        {
            target.WasObservedRunning = true;
            return;
        }

        if (!target.WasObservedRunning)
        {
            return;
        }

        var restarted = target.TryRestart();
        var verified = restarted && target.IsRunning();
        stopwatch.Stop();

        await LogGuardianAsync(
            verified ? "Success" : "Cannot Verify",
            $"{target.Name} stopped; RestartAttempted={restarted}; Verified={verified}",
            stopwatch.Elapsed,
            cancellationToken);
    }

    private Task LogGuardianAsync(string result, string technicalMessage, TimeSpan duration, CancellationToken cancellationToken)
    {
        return _logService.LogActionAsync(new ActionLogEntry(
            DateTimeOffset.Now,
            Environment.UserName,
            Environment.MachineName,
            "HealthGuardian",
            "Health Guardian",
            result,
            technicalMessage,
            null,
            result.Equals("Success", StringComparison.OrdinalIgnoreCase) || result.Equals("Started", StringComparison.OrdinalIgnoreCase) ? null : technicalMessage,
            duration), cancellationToken);
    }

    private static IReadOnlyCollection<string> GetChromeCandidates()
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

    private static IReadOnlyCollection<string> GetSlackCandidates()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return
        [
            Path.Combine(localAppData, "slack", "slack.exe"),
            Path.Combine(localAppData, "Programs", "Slack", "slack.exe")
        ];
    }

    private static IReadOnlyCollection<string> GetZoomCandidates()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return
        [
            Path.Combine(appData, "Zoom", "bin", "Zoom.exe"),
            Path.Combine(localAppData, "Programs", "Zoom", "bin", "Zoom.exe")
        ];
    }

    private static IReadOnlyCollection<string> GetGoogleDriveCandidates()
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

    private sealed class GuardianTarget(ApplicationProcessProfile profile)
    {
        private readonly ApplicationProcessManager _manager = new(profile);

        public string Name { get; } = profile.DisplayName;
        public bool WasObservedRunning { get; set; }

        public bool IsRunning()
        {
            return _manager.IsRunning();
        }

        public bool TryRestart()
        {
            return _manager.Restart(ProcessWindowStyle.Minimized).Success;
        }
    }
}
