using ZenIT.Core.Reports;

namespace ZenIT.Tests;

public sealed class ReportPrivacyTests
{
    [Fact]
    public void ReportExporter_DoesNotAddForbiddenPrivateData()
    {
        var document = new ReportDocument(
            "ZenIT Support Package",
            "1.0.0",
            DateTimeOffset.Now,
            "Device",
            "User",
            "Safe support summary",
            new Dictionary<string, object?>
            {
                ["DeviceName"] = "Device",
                ["Username"] = "User",
                ["InternetConnectivity"] = "Connected",
                ["LatestZenITActions"] = Array.Empty<string>()
            },
            "This report excludes personal files, browser history, cookies, saved passwords, tokens, chat messages, emails, Google Drive file names, and full installed software inventory.");

        var paths = new ReportExporter().Export(document, "PrivacyTest");
        var combined = File.ReadAllText(paths.TextPath) + File.ReadAllText(paths.JsonPath) + File.ReadAllText(paths.HtmlPath);

        Assert.DoesNotContain(@"Documents\", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"Downloads\", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie_value", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("saved_password", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access_token", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email body", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private chat body", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customer-drive-file.xlsx", combined, StringComparison.OrdinalIgnoreCase);
    }
}
