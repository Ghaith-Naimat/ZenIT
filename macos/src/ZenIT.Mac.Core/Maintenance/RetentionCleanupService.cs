using ZenIT.Core.Configuration;
using ZenIT.Core.Logging;

namespace ZenIT.Core.Maintenance;

public sealed class RetentionCleanupService
{
    private readonly AppSettings _settings;
    private readonly LogService _logService;

    public RetentionCleanupService(AppSettings settings, LogService logService)
    {
        _settings = settings;
        _logService = logService;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ZenITPaths.LogsDirectory);
        Directory.CreateDirectory(ZenITPaths.ReportsDirectory);

        var deletedLogs = DeleteExpiredFiles(
            ZenITPaths.LogsDirectory,
            "*.log",
            _settings.LogRetentionDays,
            keepFilePath: ZenITPaths.LogPath);
        var deletedReports = DeleteExpiredFiles(
            ZenITPaths.ReportsDirectory,
            "*.txt",
            _settings.ReportRetentionDays);

        await _logService.LogActionAsync(new ActionLogEntry(
            DateTimeOffset.Now,
            Environment.UserName,
            Environment.MachineName,
            "RetentionCleanup",
            "Retention Cleanup",
            "Success",
            $"DeletedLogs={deletedLogs}; DeletedReports={deletedReports}; LogRetentionDays={_settings.LogRetentionDays}; ReportRetentionDays={_settings.ReportRetentionDays}"),
            cancellationToken);
    }

    private static int DeleteExpiredFiles(string directory, string searchPattern, int retentionDays, string? keepFilePath = null)
    {
        if (retentionDays < 1 || !IsUnderZenITRoot(directory) || !Directory.Exists(directory))
        {
            return 0;
        }

        var cutoff = DateTimeOffset.Now.AddDays(-retentionDays);
        var deleted = 0;

        foreach (var filePath in Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly))
        {
            if (!IsUnderZenITRoot(filePath) ||
                (keepFilePath is not null && filePath.Equals(keepFilePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var lastWriteTime = File.GetLastWriteTime(filePath);
            if (lastWriteTime > cutoff)
            {
                continue;
            }

            File.Delete(filePath);
            deleted++;
        }

        return deleted;
    }

    private static bool IsUnderZenITRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetFullPath(ZenITPaths.Root);
        return fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
