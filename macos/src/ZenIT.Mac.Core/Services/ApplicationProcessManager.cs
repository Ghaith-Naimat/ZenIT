using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZenIT.Core.Services;

/// <summary>
/// macOS process control. Graceful close sends SIGTERM (no Apple Events permission needed),
/// force terminate uses SIGKILL, and restart launches the profile's .app bundle through
/// /usr/bin/open so LaunchServices applies normal app activation rules.
/// </summary>
public sealed class ApplicationProcessManager(ApplicationProcessProfile profile)
{
    private const int SigTerm = 15;

    private readonly ApplicationProcessProfile _profile = profile;

    public ApplicationProcessSnapshot GetSnapshot()
    {
        var processes = GetProcesses();
        var details = new List<string>();

        foreach (var process in processes)
        {
            try
            {
                details.Add($"Pid={process.Id}; Name={process.ProcessName}; State={SafeProcessState(process)}");
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                details.Add($"PidUnavailable; Error={exception.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        return new ApplicationProcessSnapshot(
            processes.Count > 0,
            processes.Count > 0,
            HasHiddenProcesses: false,
            HasUnresponsiveProcesses: false,
            processes.Count,
            string.Join("; ", details));
    }

    public bool IsRunning()
    {
        return GetSnapshot().IsRunning;
    }

    public ApplicationProcessOperationResult CloseGracefully(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var closeSignals = 0;

        foreach (var process in GetProcesses())
        {
            try
            {
                if (!process.HasExited && kill(process.Id, SigTerm) == 0)
                {
                    closeSignals++;
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Process may exit while being inspected. Aggregate state is reported below.
            }
            finally
            {
                process.Dispose();
            }
        }

        var stopped = WaitUntilStopped(timeout);
        stopwatch.Stop();
        return new ApplicationProcessOperationResult(
            stopped,
            $"GracefulCloseSignals={closeSignals}; Signal=SIGTERM; Stopped={stopped}",
            stopwatch.Elapsed);
    }

    public ApplicationProcessOperationResult ForceTerminate(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var killed = 0;
        var failures = new List<string>();

        for (var attempt = 1; attempt <= 2 && IsRunning(); attempt++)
        {
            foreach (var process in GetProcesses())
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        killed++;
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    failures.Add($"Pid={SafeProcessId(process)}; Attempt={attempt}; Error={exception.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }

            _ = WaitUntilStopped(timeout);
        }

        var stopped = !IsRunning();
        stopwatch.Stop();
        return new ApplicationProcessOperationResult(
            stopped,
            $"ForceKillUsed=True; KilledProcesses={killed}; Stopped={stopped}; Failures={string.Join(" | ", failures)}",
            stopwatch.Elapsed);
    }

    public ApplicationProcessOperationResult WaitForExit(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var stopped = WaitUntilStopped(timeout);
        stopwatch.Stop();
        return new ApplicationProcessOperationResult(stopped, $"Stopped={stopped}", stopwatch.Elapsed);
    }

    public ApplicationProcessOperationResult Restart(ProcessWindowStyle windowStyle = ProcessWindowStyle.Normal)
    {
        var stopwatch = Stopwatch.StartNew();
        var bundle = _profile.ExecutableCandidates.FirstOrDefault(Directory.Exists);
        if (string.IsNullOrWhiteSpace(bundle))
        {
            stopwatch.Stop();
            return new ApplicationProcessOperationResult(false, "ExecutableFound=False", stopwatch.Elapsed);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (windowStyle == ProcessWindowStyle.Minimized)
            {
                startInfo.ArgumentList.Add("-g");
                startInfo.ArgumentList.Add("-j");
            }

            startInfo.ArgumentList.Add(bundle);
            using var process = Process.Start(startInfo);

            stopwatch.Stop();
            return new ApplicationProcessOperationResult(process is not null, $"Bundle={bundle}; Started={process is not null}", stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            stopwatch.Stop();
            return new ApplicationProcessOperationResult(false, $"Bundle={bundle}; Error={exception.Message}", stopwatch.Elapsed);
        }
    }

    public ApplicationProcessOperationResult VerifyRunning(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var deadline = DateTimeOffset.Now.Add(timeout);
        ApplicationProcessSnapshot snapshot;
        do
        {
            snapshot = GetSnapshot();
            if (snapshot.IsRunning)
            {
                stopwatch.Stop();
                return new ApplicationProcessOperationResult(true, $"VerifiedRunning=True; {snapshot.TechnicalMessage}", stopwatch.Elapsed);
            }

            Thread.Sleep(250);
        }
        while (DateTimeOffset.Now < deadline);

        stopwatch.Stop();
        return new ApplicationProcessOperationResult(false, "VerifiedRunning=False", stopwatch.Elapsed);
    }

    public ApplicationProcessOperationResult VerifyStopped(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var stopped = WaitUntilStopped(timeout);
        stopwatch.Stop();
        return new ApplicationProcessOperationResult(stopped, $"VerifiedStopped={stopped}", stopwatch.Elapsed);
    }

    private bool WaitUntilStopped(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now.Add(timeout);
        while (DateTimeOffset.Now < deadline)
        {
            if (!IsRunning())
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return !IsRunning();
    }

    private List<Process> GetProcesses()
    {
        var processes = new Dictionary<int, Process>();
        foreach (var process in Process.GetProcesses())
        {
            var name = MacSystemInfo.SafeProcessName(process);
            if (_profile.ProcessNames.Any(target => MacSystemInfo.MatchesProcessName(name, target)))
            {
                if (!processes.TryAdd(process.Id, process))
                {
                    process.Dispose();
                }
            }
            else
            {
                process.Dispose();
            }
        }

        return processes.Values.ToList();
    }

    private static int SafeProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static string SafeProcessState(Process process)
    {
        try
        {
            return process.HasExited ? "Exited" : "Running";
        }
        catch (InvalidOperationException)
        {
            return "Exited";
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signal);
}
