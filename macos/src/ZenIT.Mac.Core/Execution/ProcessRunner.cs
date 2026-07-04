using System.Diagnostics;
using ZenIT.Core.Configuration;

namespace ZenIT.Core.Execution;

public sealed class ProcessRunner
{
    private static readonly IReadOnlySet<string> AllowedCommands = new HashSet<string>(StringComparer.Ordinal)
    {
        "open",
        "dscacheutil"
    };

    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ValidateAllowedCommand(fileName, arguments);

        var startedAt = DateTimeOffset.Now;
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        if (fileName.Equals("open", StringComparison.Ordinal))
        {
            process.StartInfo.ArgumentList.Add(arguments.Trim().Trim('"'));
        }
        else
        {
            process.StartInfo.Arguments = arguments;
        }

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        var finishedAt = DateTimeOffset.Now;
        return new ProcessRunResult(
            fileName,
            arguments,
            process.ExitCode,
            await outputTask,
            await errorTask,
            startedAt,
            finishedAt);
    }

    private static void ValidateAllowedCommand(string fileName, string arguments)
    {
        if (!AllowedCommands.Contains(fileName))
        {
            throw new InvalidOperationException("This command is not allowed by ZenIT.");
        }

        if (fileName.Equals("dscacheutil", StringComparison.Ordinal) &&
            !arguments.Trim().Equals("-flushcache", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("This dscacheutil operation is not allowed by ZenIT.");
        }

        if (fileName.Equals("open", StringComparison.Ordinal))
        {
            var normalized = arguments.Trim().Trim('"');
            var allowedFolders = new[]
            {
                ZenITPaths.LogsDirectory,
                ZenITPaths.ReportsDirectory
            };

            if (!allowedFolders.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This folder is not allowed by ZenIT.");
            }
        }
    }
}
