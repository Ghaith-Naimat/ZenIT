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
            new(MacApplicationProfiles.Chrome),
            new(MacApplicationProfiles.Slack),
            new(MacApplicationProfiles.Zoom),
            new(MacApplicationProfiles.GoogleDrive),
            new(MacApplicationProfiles.JumpCloud)
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
