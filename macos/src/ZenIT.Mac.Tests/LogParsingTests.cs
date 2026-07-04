using ZenIT.Core.Logging;

namespace ZenIT.Mac.Tests;

public sealed class LogParsingTests
{
    [Fact]
    public async Task LogService_ParsesNewFormatAndSkipsCorruptLines()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"ZenIT-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var logPath = Path.Combine(tempDirectory, "ZenIT.log");
        var service = new LogService(logPath);

        await service.LogActionAsync(new ActionLogEntry(
            DateTimeOffset.Now,
            "user",
            "device",
            "WorkflowId",
            "Workflow Name",
            "Success",
            "technical",
            Duration: TimeSpan.FromSeconds(3.4)));
        await File.AppendAllTextAsync(logPath, "not a valid log line" + Environment.NewLine);

        var summaries = service.GetLatestSummaries(10);

        Assert.Contains(summaries, summary => summary.ActionName == "Workflow Name" && summary.Duration.HasValue);
        Assert.True(service.LastParseErrorCount >= 1);
    }
}
