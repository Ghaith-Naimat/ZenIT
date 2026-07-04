using System.Text;
using ZenIT.Core.Configuration;

namespace ZenIT.Core.Logging;

public sealed class LogService
{
    public const string DefaultLogPath = ZenITPaths.LogPath;
    private const long MaxLogFileSizeBytes = 10L * 1024L * 1024L;
    private const int MaxRotatedFiles = 5;

    private readonly string _logPath;
    private string _activeLogPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _cacheLock = new();
    private IReadOnlyList<ActionLogSummary>? _cachedSummaries;
    private string _cachedSignature = string.Empty;

    public LogService(string logPath = DefaultLogPath)
    {
        _logPath = logPath;
        _activeLogPath = logPath;
    }

    public string PrimaryLogPath => _logPath;
    public string FallbackLogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ZenIT",
        "Logs",
        "ZenIT.log");
    public string ActiveLogPath => _activeLogPath;
    public bool IsUsingFallback => !_activeLogPath.Equals(_logPath, StringComparison.OrdinalIgnoreCase);
    public int LastParseErrorCount { get; private set; }

    public async Task LogActionAsync(ActionLogEntry entry, CancellationToken cancellationToken = default)
    {
        var line = string.Join(" | ",
            entry.Timestamp.ToString("O"),
            $"user={Clean(entry.WindowsUsername)}",
            $"device={Clean(entry.DeviceName)}",
            $"actionId={Clean(entry.ActionId)}",
            $"action={Clean(entry.ActionName)}",
            $"result={Clean(entry.Result)}",
            $"durationMs={(entry.Duration.HasValue ? entry.Duration.Value.TotalMilliseconds.ToString("0") : string.Empty)}",
            $"technical={Clean(entry.TechnicalMessage ?? string.Empty)}",
            $"reportPath={Clean(entry.ReportPath ?? string.Empty)}",
            $"error={Clean(entry.ErrorMessage ?? string.Empty)}");

        try
        {
            await _writeLock.WaitAsync(cancellationToken);
        }
        catch
        {
            return;
        }

        try
        {
            if (!await TryAppendLineAsync(_activeLogPath, line, cancellationToken))
            {
                _activeLogPath = FallbackLogPath;
                await TryAppendLineAsync(_activeLogPath, line, cancellationToken);
            }

            await TryAppendTypedLogAsync(entry, line, cancellationToken);
            InvalidateSummaryCache();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public IReadOnlyList<ActionLogSummary> GetLatestSummaries(int count)
    {
        var signature = GetLogSignature();
        lock (_cacheLock)
        {
            if (_cachedSummaries is not null && _cachedSignature == signature)
            {
                return _cachedSummaries.Take(count).ToList();
            }
        }

        var summaries = new List<ActionLogSummary>();
        LastParseErrorCount = 0;
        foreach (var logPath in GetCandidateLogPaths())
        {
            if (!File.Exists(logPath))
            {
                continue;
            }

            try
            {
                foreach (var line in File.ReadLines(logPath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var summary = TryParseSummary(line);
                    if (summary is null)
                    {
                        LastParseErrorCount++;
                        continue;
                    }

                    summaries.Add(summary);
                }
            }
            catch
            {
                LastParseErrorCount++;
                continue;
            }
        }

        var orderedSummaries = summaries
            .OrderByDescending(summary => summary.Timestamp)
            .ToList();

        lock (_cacheLock)
        {
            _cachedSignature = signature;
            _cachedSummaries = orderedSummaries;
        }

        return orderedSummaries.Take(count).ToList();
    }

    public string GetLatestSummaryText(int count)
    {
        var summaries = GetLatestSummaries(count);
        if (summaries.Count == 0)
        {
            return "No ZenIT support history yet.";
        }

        return string.Join(
            Environment.NewLine,
            summaries.Select(summary => $"{summary.Timestamp:g} - {summary.ActionName} - {summary.Result}"));
    }

    public string GetLogFolderPath()
    {
        return Path.GetDirectoryName(_activeLogPath) ?? ZenITPaths.LogsDirectory;
    }

    private async Task<bool> TryAppendLineAsync(string path, string line, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            RotateIfNeeded(path);
            await File.AppendAllTextAsync(path, line + Environment.NewLine, Encoding.UTF8, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task TryAppendTypedLogAsync(ActionLogEntry entry, string line, CancellationToken cancellationToken)
    {
        var fileName = GetTypedLogFileName(entry);
        var directory = Path.GetDirectoryName(_activeLogPath) ?? ZenITPaths.LogsDirectory;
        var typedPath = Path.Combine(directory, fileName);
        if (!await TryAppendLineAsync(typedPath, line, cancellationToken))
        {
            var fallbackPath = Path.Combine(Path.GetDirectoryName(FallbackLogPath) ?? ZenITPaths.LogsDirectory, fileName);
            await TryAppendLineAsync(fallbackPath, line, cancellationToken);
        }

        if (IsError(entry))
        {
            var errorPath = Path.Combine(directory, "Errors.log");
            if (!await TryAppendLineAsync(errorPath, line, cancellationToken))
            {
                var fallbackErrorPath = Path.Combine(Path.GetDirectoryName(FallbackLogPath) ?? ZenITPaths.LogsDirectory, "Errors.log");
                await TryAppendLineAsync(fallbackErrorPath, line, cancellationToken);
            }
        }
    }

    private static string GetTypedLogFileName(ActionLogEntry entry)
    {
        if (entry.ActionId.Contains("ITMode", StringComparison.OrdinalIgnoreCase) ||
            entry.ActionName.Contains("IT Mode", StringComparison.OrdinalIgnoreCase))
        {
            return "ITMode.log";
        }

        if (entry.ActionId.Contains("Startup", StringComparison.OrdinalIgnoreCase) ||
            entry.ActionId.Contains("Cleanup", StringComparison.OrdinalIgnoreCase) ||
            entry.ActionId.Contains("Validation", StringComparison.OrdinalIgnoreCase))
        {
            return "System.log";
        }

        if (entry.ActionId.Contains("HealthGuardian", StringComparison.OrdinalIgnoreCase) ||
            entry.ActionName.Contains("Health Guardian", StringComparison.OrdinalIgnoreCase))
        {
            return "HealthGuardian.log";
        }

        return "Workflow.log";
    }

    private static bool IsError(ActionLogEntry entry)
    {
        return !string.IsNullOrWhiteSpace(entry.ErrorMessage) ||
               entry.Result.Contains("Fail", StringComparison.OrdinalIgnoreCase) ||
               entry.Result.Contains("Error", StringComparison.OrdinalIgnoreCase);
    }

    private static void RotateIfNeeded(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length < MaxLogFileSizeBytes)
        {
            return;
        }

        for (var index = MaxRotatedFiles; index >= 1; index--)
        {
            var source = $"{path}.{index}";
            var destination = $"{path}.{index + 1}";
            if (index == MaxRotatedFiles && File.Exists(source))
            {
                File.Delete(source);
                continue;
            }

            if (File.Exists(source))
            {
                File.Move(source, destination, overwrite: true);
            }
        }

        File.Move(path, $"{path}.1", overwrite: true);
    }

    private IEnumerable<string> GetCandidateLogPaths()
    {
        var primaryDirectory = Path.GetDirectoryName(_logPath) ?? ZenITPaths.LogsDirectory;
        var fallbackDirectory = Path.GetDirectoryName(FallbackLogPath) ??
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZenIT", "Logs");
        var activeDirectory = Path.GetDirectoryName(_activeLogPath) ?? primaryDirectory;
        var typedLogNames = new[]
        {
            "Workflow.log",
            "ITMode.log",
            "System.log",
            "HealthGuardian.log",
            "Errors.log"
        };

        return new[] { _logPath, FallbackLogPath, _activeLogPath }
            .Concat(new[] { primaryDirectory, fallbackDirectory, activeDirectory }
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(directory => typedLogNames.Select(name => Path.Combine(directory, name))))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private string GetLogSignature()
    {
        var builder = new StringBuilder();
        foreach (var path in GetCandidateLogPaths())
        {
            try
            {
                var file = new FileInfo(path);
                if (file.Exists)
                {
                    builder.Append(path)
                        .Append('|')
                        .Append(file.Length)
                        .Append('|')
                        .Append(file.LastWriteTimeUtc.Ticks)
                        .Append(';');
                }
            }
            catch
            {
                builder.Append(path).Append("|unavailable;");
            }
        }

        return builder.ToString();
    }

    private void InvalidateSummaryCache()
    {
        lock (_cacheLock)
        {
            _cachedSummaries = null;
            _cachedSignature = string.Empty;
        }
    }

    private static ActionLogSummary? TryParseSummary(string line)
    {
        var parts = line.Split(" | ");
        if (parts.Length == 0 || !DateTimeOffset.TryParse(parts[0], out var timestamp))
        {
            return null;
        }

        var values = parts
            .Skip(1)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.OrdinalIgnoreCase);

        values.TryGetValue("action", out var action);
        values.TryGetValue("workflow", out var workflow);
        values.TryGetValue("workflowName", out var workflowName);
        values.TryGetValue("actionId", out var actionId);
        values.TryGetValue("workflowId", out var workflowId);
        values.TryGetValue("result", out var result);
        values.TryGetValue("durationMs", out var durationText);
        values.TryGetValue("duration", out var friendlyDurationText);
        values.TryGetValue("reportPath", out var reportPath);
        TimeSpan? duration = null;
        if (double.TryParse(durationText, out var durationMs))
        {
            duration = TimeSpan.FromMilliseconds(durationMs);
        }
        else if (TimeSpan.TryParse(friendlyDurationText, out var parsedDuration))
        {
            duration = parsedDuration;
        }

        return new ActionLogSummary(
            timestamp,
            FirstNonEmpty(actionId, workflowId),
            FirstNonEmpty(action, workflow, workflowName, "ZenIT action"),
            string.IsNullOrWhiteSpace(result) ? "Unknown" : result,
            duration,
            string.IsNullOrWhiteSpace(reportPath) ? null : reportPath);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string Clean(string value)
    {
        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }
}
