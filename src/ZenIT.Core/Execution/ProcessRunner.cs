using System.Diagnostics;
using ZenIT.Core.Configuration;

namespace ZenIT.Core.Execution;

public sealed class ProcessRunner
{
    private static readonly IReadOnlySet<string> AllowedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ipconfig",
        "explorer.exe"
    };

    private static readonly IReadOnlySet<string> AllowedIpConfigArguments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "/flushdns",
        "/release",
        "/renew",
        "/registerdns"
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
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

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

        if (fileName.Equals("ipconfig", StringComparison.OrdinalIgnoreCase) &&
            !AllowedIpConfigArguments.Contains(arguments))
        {
            throw new InvalidOperationException("This ipconfig operation is not allowed by ZenIT.");
        }

        if (fileName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
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
