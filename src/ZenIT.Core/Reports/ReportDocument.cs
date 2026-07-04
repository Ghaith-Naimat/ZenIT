namespace ZenIT.Core.Reports;

public sealed record ReportDocument(
    string Title,
    string Version,
    DateTimeOffset Timestamp,
    string Device,
    string User,
    string Summary,
    IReadOnlyDictionary<string, object?> Details,
    string Footer);
