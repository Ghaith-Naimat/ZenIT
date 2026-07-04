namespace ZenIT.Core.Configuration;

public static class AppSettingsValidator
{
    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        var issues = new List<string>();

        if (settings.AppMode is not ("Pilot" or "Production" or "Testing" or "Development"))
        {
            issues.Add($"Invalid AppMode '{settings.AppMode}'.");
        }

        if (settings.UpdateChannel is not ("Production" or "Testing" or "Development"))
        {
            issues.Add($"Invalid UpdateChannel '{settings.UpdateChannel}'.");
        }

        if (settings.Language is not ("en" or "ar"))
        {
            issues.Add($"Unsupported Language '{settings.Language}'.");
        }

        if (!ThemeManager.IsSupported(settings.Theme))
        {
            issues.Add($"Unsupported Theme '{settings.Theme}'.");
        }

        if (settings.LogRetentionDays is < 1 or > 365)
        {
            issues.Add("LogRetentionDays must be between 1 and 365.");
        }

        if (settings.ReportRetentionDays is < 1 or > 365)
        {
            issues.Add("ReportRetentionDays must be between 1 and 365.");
        }

        return issues;
    }
}
