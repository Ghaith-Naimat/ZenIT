using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ZenIT.Core.Services;

/// <summary>
/// Read-only macOS system probes. Every probe shells out to a fixed, argument-controlled
/// diagnostic binary (sw_vers, pmset, sysctl, vm_stat, system_profiler, ps) and never
/// accepts caller-provided command text.
/// </summary>
public static class MacSystemInfo
{
    private static readonly object CacheLock = new();
    private static string? _cachedOSVersion;
    private static (string SerialNumber, string Manufacturer, string Model)? _cachedHardwareInfo;
    private static string? _cachedCpuName;

    public static string GetMacOSVersionString()
    {
        lock (CacheLock)
        {
            if (_cachedOSVersion is not null)
            {
                return _cachedOSVersion;
            }
        }

        var product = RunCapture("/usr/bin/sw_vers", "-productVersion")?.Trim();
        var build = RunCapture("/usr/bin/sw_vers", "-buildVersion")?.Trim();
        var version = string.IsNullOrWhiteSpace(product)
            ? Environment.OSVersion.VersionString
            : $"macOS {product}{(string.IsNullOrWhiteSpace(build) ? string.Empty : $" (build {build})")}";

        lock (CacheLock)
        {
            _cachedOSVersion = version;
        }

        return version;
    }

    public static string GetMacOSBuild()
    {
        return RunCapture("/usr/bin/sw_vers", "-buildVersion")?.Trim() ?? "Not available";
    }

    public static int? GetBatteryPercentage()
    {
        var output = RunCapture("/usr/bin/pmset", "-g batt");
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var match = Regex.Match(output, @"(\d{1,3})%");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var percentage))
        {
            return null;
        }

        return percentage is >= 0 and <= 100 ? percentage : null;
    }

    public static (string SerialNumber, string Manufacturer, string Model) GetHardwareInfo()
    {
        lock (CacheLock)
        {
            if (_cachedHardwareInfo is not null)
            {
                return _cachedHardwareInfo.Value;
            }
        }

        var output = RunCapture("/usr/sbin/system_profiler", "SPHardwareDataType -detailLevel mini", timeoutMilliseconds: 15_000) ?? string.Empty;
        var serial = MatchProfilerValue(output, "Serial Number (system)");
        var model = MatchProfilerValue(output, "Model Name");
        var modelIdentifier = MatchProfilerValue(output, "Model Identifier");
        var info = (
            string.IsNullOrWhiteSpace(serial) ? "Not available" : serial,
            "Apple",
            string.IsNullOrWhiteSpace(model)
                ? string.IsNullOrWhiteSpace(modelIdentifier) ? "Not available" : modelIdentifier
                : $"{model}{(string.IsNullOrWhiteSpace(modelIdentifier) ? string.Empty : $" ({modelIdentifier})")}");

        lock (CacheLock)
        {
            _cachedHardwareInfo = info;
        }

        return info;
    }

    public static string GetCpuName()
    {
        lock (CacheLock)
        {
            if (_cachedCpuName is not null)
            {
                return _cachedCpuName;
            }
        }

        var name = RunCapture("/usr/sbin/sysctl", "-n machdep.cpu.brand_string")?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Not available";
        }

        lock (CacheLock)
        {
            _cachedCpuName = name;
        }

        return name;
    }

    public static (double TotalBytes, double AvailableBytes, double AvailablePercent) GetMemoryStatus()
    {
        var totalText = RunCapture("/usr/sbin/sysctl", "-n hw.memsize")?.Trim();
        if (!double.TryParse(totalText, NumberStyles.Any, CultureInfo.InvariantCulture, out var total) || total <= 0)
        {
            return (0, 0, 0);
        }

        var vmStat = RunCapture("/usr/bin/vm_stat") ?? string.Empty;
        var pageSize = 4096d;
        var pageSizeMatch = Regex.Match(vmStat, @"page size of (\d+) bytes");
        if (pageSizeMatch.Success && double.TryParse(pageSizeMatch.Groups[1].Value, out var parsedPageSize))
        {
            pageSize = parsedPageSize;
        }

        var available = (ReadVmStatPages(vmStat, "Pages free") +
                         ReadVmStatPages(vmStat, "Pages inactive") +
                         ReadVmStatPages(vmStat, "Pages speculative")) * pageSize;
        if (available <= 0)
        {
            return (total, 0, 50);
        }

        return (total, available, Math.Clamp(available / total * 100, 0, 100));
    }

    public static double? GetCpuUsageEstimate()
    {
        var output = RunCapture("/bin/ps", "-A -o %cpu");
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        double sum = 0;
        var parsedAny = false;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (double.TryParse(line, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                sum += value;
                parsedAny = true;
            }
        }

        if (!parsedAny)
        {
            return null;
        }

        var cores = Math.Max(1, Environment.ProcessorCount);
        return Math.Clamp(sum / cores, 0, 100);
    }

    public static int CountRecentDiagnosticReports(int days)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Logs",
                "DiagnosticReports");
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            var cutoff = DateTime.Now.AddDays(-days);
            return Directory.EnumerateFiles(directory)
                .Count(file => File.GetLastWriteTime(file) >= cutoff);
        }
        catch
        {
            return 0;
        }
    }

    public static IReadOnlyList<string> GetFailedLaunchServices(int maxNames = 10)
    {
        var output = RunCapture("/bin/launchctl", "list");
        if (string.IsNullOrWhiteSpace(output))
        {
            return ["launchctl list unavailable"];
        }

        var failed = new List<string>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 &&
                int.TryParse(parts[1], out var status) &&
                status != 0)
            {
                failed.Add($"{parts[2]} (exit {status})");
            }
        }

        if (failed.Count == 0)
        {
            return ["None detected"];
        }

        var summary = failed.Take(maxNames).ToList();
        if (failed.Count > maxNames)
        {
            summary.Add($"and {failed.Count - maxNames} more");
        }

        return summary;
    }

    public static bool IsAnyProcessRunning(IReadOnlyCollection<string> processNames)
    {
        try
        {
            var runningNames = Process.GetProcesses().Select(SafeProcessName).ToList();
            return processNames.Any(target => runningNames.Any(running => MatchesProcessName(running, target)));
        }
        catch
        {
            return false;
        }
    }

    public static int CountProcessesContaining(string partialName)
    {
        try
        {
            return Process.GetProcesses().Count(process =>
                SafeProcessName(process).Contains(partialName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Kernel process tables may truncate long executable names, so match on either side's prefix.
    /// </summary>
    public static bool MatchesProcessName(string runningName, string targetName)
    {
        if (string.IsNullOrWhiteSpace(runningName) || string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        return runningName.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
               targetName.StartsWith(runningName, StringComparison.OrdinalIgnoreCase) ||
               runningName.StartsWith(targetName, StringComparison.OrdinalIgnoreCase);
    }

    public static string SafeProcessName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    public static string? RunCapture(string fileName, string arguments = "", int timeoutMilliseconds = 10_000)
    {
        try
        {
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

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            return output;
        }
        catch
        {
            return null;
        }
    }

    private static double ReadVmStatPages(string vmStat, string label)
    {
        var match = Regex.Match(vmStat, $@"{Regex.Escape(label)}:\s+(\d+)");
        return match.Success && double.TryParse(match.Groups[1].Value, out var pages) ? pages : 0;
    }

    private static string MatchProfilerValue(string output, string label)
    {
        var match = Regex.Match(output, $@"{Regex.Escape(label)}:\s*(.+)");
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}
