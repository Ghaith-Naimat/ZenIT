using System.Net;
using System.Text;
using System.Text.Json;
using ZenIT.Core.Configuration;
using ZenIT.Core.Logging;

namespace ZenIT.Core.Reports;

public sealed class ReportExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ZenITPathProvider _paths;

    public ReportExporter(ZenITPathProvider? paths = null)
    {
        _paths = paths ?? ZenITPathProvider.CreateProduction();
    }

    private static readonly IReadOnlyDictionary<string, string[]> HtmlSections = new Dictionary<string, string[]>
    {
        ["Device"] = ["DeviceName", "Username", "SerialNumber", "Manufacturer", "Model", "MacOSVersion", "MacOSBuild", "LastRebootTime", "Uptime"],
        ["Network"] = ["IpAddress", "MacAddress", "Gateway", "DnsServers", "InternetConnectivity"],
        ["Performance"] = ["CpuName", "CpuUsage", "RamTotal", "RamAvailablePercent", "DiskTotal", "DiskFree", "DiskFreePercent", "Battery"],
        ["Security"] = ["FirewallStatus", "FileVaultStatus", "GatekeeperStatus", "SipStatus", "MdmJumpCloudStatus"],
        ["macOS Health"] = ["PendingUpdatesSummary", "FailedServices", "CrashReportsLast7Days"],
        ["ZenIT Activity"] = ["LatestZenITActions"]
    };

    public ReportExportPaths Export(ReportDocument document, string prefix)
    {
        Directory.CreateDirectory(_paths.ReportsDirectory);
        var basePath = Path.Combine(
            _paths.ReportsDirectory,
            $"{Sanitize(prefix)}-{Sanitize(document.Device)}-{Sanitize(document.User)}-{document.Timestamp:yyyyMMdd-HHmmss}");

        var paths = new ReportExportPaths(basePath + ".txt", basePath + ".json", basePath + ".html");
        File.WriteAllText(paths.TextPath, BuildText(document), Encoding.UTF8);
        File.WriteAllText(paths.JsonPath, JsonSerializer.Serialize(document, JsonOptions), Encoding.UTF8);
        File.WriteAllText(paths.HtmlPath, BuildHtml(document), Encoding.UTF8);
        return paths;
    }

    private static string BuildText(ReportDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine(document.Title);
        builder.AppendLine(new string('=', document.Title.Length));
        builder.AppendLine($"Timestamp: {document.Timestamp:O}");
        builder.AppendLine($"ZenIT Version: {document.Version}");
        builder.AppendLine($"Device: {document.Device}");
        builder.AppendLine($"User: {document.User}");
        builder.AppendLine($"Summary: {document.Summary}");
        builder.AppendLine();
        builder.AppendLine("Details");
        builder.AppendLine("-------");
        foreach (var item in document.Details)
        {
            builder.AppendLine($"{item.Key}: {FormatValue(item.Value)}");
        }

        builder.AppendLine();
        builder.AppendLine(document.Footer);
        return builder.ToString();
    }

    private static string BuildHtml(ReportDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>ZenIT Support Package</title>");
        builder.AppendLine("<style>body{font-family:-apple-system,Helvetica Neue,Arial,sans-serif;background:#EEF6FF;color:#172B3A;margin:0;padding:32px}.wrap{max-width:1040px;margin:auto}.card{background:#fff;border:1px solid #DCEAF4;border-radius:20px;padding:22px;margin:18px 0;box-shadow:0 8px 24px rgba(23,43,58,.08)}h1{margin:0;color:#006D73}.muted{color:#6B7A8A}h2{color:#172B3A}.row{display:grid;grid-template-columns:minmax(180px,240px) 1fr;border-top:1px solid #EEF6FF;padding:10px 0}.label{color:#6B7A8A;font-weight:600}.value{font-weight:600;white-space:pre-wrap;overflow-wrap:anywhere}@media(max-width:720px){.row{grid-template-columns:1fr}}</style>");
        builder.AppendLine("</head><body><div class=\"wrap\">");
        builder.AppendLine($"<h1>{WebUtility.HtmlEncode(document.Title)}</h1>");
        builder.AppendLine($"<p class=\"muted\">{WebUtility.HtmlEncode(document.Summary)} | {WebUtility.HtmlEncode(document.Timestamp.ToString("g"))} | ZenIT {WebUtility.HtmlEncode(document.Version)}</p>");

        foreach (var section in HtmlSections)
        {
            builder.AppendLine($"<div class=\"card\"><h2>{WebUtility.HtmlEncode(section.Key)}</h2>");
            foreach (var key in section.Value)
            {
                document.Details.TryGetValue(key, out var value);
                builder.AppendLine($"<div class=\"row\"><div class=\"label\">{WebUtility.HtmlEncode(key)}</div><div class=\"value\">{WebUtility.HtmlEncode(FormatValue(value))}</div></div>");
            }

            builder.AppendLine("</div>");
        }

        builder.AppendLine($"<div class=\"card\"><strong>Privacy:</strong> {WebUtility.HtmlEncode(document.Footer)}</div>");
        builder.AppendLine("</div></body></html>");
        return builder.ToString();
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "Not available";
        }

        if (value is IEnumerable<string> strings)
        {
            return string.Join(", ", strings);
        }

        if (value is IEnumerable<ActionLogSummary> summaries)
        {
            return string.Join(Environment.NewLine, summaries.Select(summary =>
                $"{summary.Timestamp:g} - {summary.ActionName} - {summary.Result} - Duration={(summary.Duration.HasValue ? FormatDuration(summary.Duration.Value) : "Not available")}"));
        }

        if (value is IEnumerable<object> objects && value is not string)
        {
            return string.Join(Environment.NewLine, objects.Select(item => item.ToString()));
        }

        return value.ToString() ?? "Not available";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds:0.0} seconds"
            : $"{duration.TotalMinutes:0.0} minutes";
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Unknown" : cleaned.Trim();
    }
}
