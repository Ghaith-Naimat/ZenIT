using ZenIT.Core.Logging;

namespace ZenIT.Mac.App.ViewModels;

public sealed class LogRowViewModel
{
    public LogRowViewModel(ActionLogSummary summary)
    {
        Timestamp = summary.Timestamp;
        ActionId = summary.ActionId;
        Time = summary.Timestamp.ToString("MMM d, h:mm tt");
        Action = summary.ActionName;
        Result = summary.Result;
        Duration = summary.Duration.HasValue ? FormatDuration(summary.Duration.Value) : "-";
    }

    public DateTimeOffset Timestamp { get; }
    public string ActionId { get; }
    public string Time { get; }
    public string Action { get; }
    public string Result { get; }
    public string Duration { get; }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds:0.0}s"
            : $"{duration.TotalMinutes:0.0}m";
    }
}
