using ZenIT.Core.Logging;

namespace ZenIT.Mac.App.ViewModels;

public sealed class RecentActivityViewModel
{
    public RecentActivityViewModel(ActionLogSummary summary, Func<string, string>? localize = null)
    {
        localize ??= key => key;
        Icon = summary.Result.Contains("success", StringComparison.OrdinalIgnoreCase) ? "OK" : "!";
        Title = summary.ActionName;
        TimeText = FormatRelativeTime(summary.Timestamp, localize);
        Brush = summary.Result.Contains("success", StringComparison.OrdinalIgnoreCase) ? "#20B486" : "#F5A623";
        Duration = summary.Duration.HasValue ? FormatDuration(summary.Duration.Value, localize) : string.Empty;
    }

    public string Icon { get; }
    public string Title { get; }
    public string TimeText { get; }
    public string Brush { get; }
    public string Duration { get; }

    private static string FormatRelativeTime(DateTimeOffset timestamp, Func<string, string> localize)
    {
        var local = timestamp.ToLocalTime();
        var now = DateTimeOffset.Now;
        if (local.Date == now.Date)
        {
            return $"{localize("Time.Today")} {local:h:mm tt}";
        }

        if (local.Date == now.AddDays(-1).Date)
        {
            return localize("Time.Yesterday");
        }

        return local.ToString("MMM d, h:mm tt");
    }

    private static string FormatDuration(TimeSpan duration, Func<string, string> localize)
    {
        return duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds:0.0} {localize("Time.SecondsShort")}"
            : $"{duration.TotalMinutes:0.0} {localize("Time.MinutesShort")}";
    }
}
