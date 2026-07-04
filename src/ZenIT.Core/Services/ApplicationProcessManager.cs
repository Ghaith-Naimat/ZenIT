using System.Diagnostics;

namespace ZenIT.Core.Services;

public sealed class ApplicationProcessManager(ApplicationProcessProfile profile)
{
    private readonly ApplicationProcessProfile _profile = profile;

    public ApplicationProcessSnapshot GetSnapshot()
    {
        var processes = GetProcesses();
        var details = new List<string>();
        var visible = false;
        var hidden = false;
        var unresponsive = false;

        foreach (var process in processes)
        {
            try
            {
                var hasWindow = process.MainWindowHandle != IntPtr.Zero;
                visible |= hasWindow;
                hidden |= !hasWindow;
                var responding = !hasWindow || process.Responding;
                unresponsive |= !responding;
                details.Add($"Pid={process.Id}; Name={process.ProcessName}; Window={hasWindow}; Responding={responding}; State={SafeProcessState(process)}");
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
            visible,
            hidden,
            unresponsive,
            processes.Count,
            string.Join("; ", details));
    }

    public bool IsRunning()
    {
        return GetSnapshot().IsRunning;
    }

    public bool HasHiddenProcesses()
    {
        return GetSnapshot().HasHiddenProcesses;
    }

    public ApplicationProcessOperationResult CloseGracefully(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var closeSignals = 0;

        foreach (var process in GetProcesses())
        {
            try
            {
                if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero && process.CloseMainWindow())
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
            $"GracefulCloseSignals={closeSignals}; Stopped={stopped}",
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
        var executable = _profile.ExecutableCandidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(executable))
        {
            stopwatch.Stop();
            return new ApplicationProcessOperationResult(false, "ExecutableFound=False", stopwatch.Elapsed);
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                WindowStyle = windowStyle
            });

            stopwatch.Stop();
            return new ApplicationProcessOperationResult(process is not null, $"Executable={executable}; Started={process is not null}", stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            stopwatch.Stop();
            return new ApplicationProcessOperationResult(false, $"Executable={executable}; Error={exception.Message}", stopwatch.Elapsed);
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
        foreach (var processName in _profile.ProcessNames.Select(Path.GetFileNameWithoutExtension).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!processes.TryAdd(process.Id, process))
                    {
                        process.Dispose();
                    }
                }
                catch (InvalidOperationException)
                {
                    process.Dispose();
                }
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
}
