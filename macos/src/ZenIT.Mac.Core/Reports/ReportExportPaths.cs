namespace ZenIT.Core.Reports;

public sealed record ReportExportPaths(string TextPath, string JsonPath, string HtmlPath)
{
    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["txt"] = TextPath,
            ["json"] = JsonPath,
            ["html"] = HtmlPath
        };
    }
}
