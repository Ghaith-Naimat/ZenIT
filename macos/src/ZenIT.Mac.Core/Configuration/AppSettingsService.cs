using System.Text.Json;

namespace ZenIT.Core.Configuration;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly ZenITPathProvider _paths;

    public AppSettingsService(string? settingsPath = null, ZenITPathProvider? paths = null)
    {
        _paths = paths ?? ZenITPathProvider.CreateProduction();
        _settingsPath = settingsPath ?? _paths.SettingsPath;
    }

    public AppSettings LoadOrCreate()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        Directory.CreateDirectory(_paths.LogsDirectory);
        Directory.CreateDirectory(_paths.ReportsDirectory);

        if (!File.Exists(_settingsPath))
        {
            var defaults = new AppSettings();
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(defaults, JsonOptions));
            return defaults;
        }

        var rawSettings = File.ReadAllText(_settingsPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(rawSettings, JsonOptions);
        var normalizedSettings = Normalize(settings ?? new AppSettings());
        if (!normalizedSettings.Equals(settings) || ContainsLegacyProtectedPolicyFields(rawSettings))
        {
            Save(normalizedSettings);
        }

        return normalizedSettings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        AtomicJsonFile.Write(_settingsPath, settings, JsonOptions);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        return settings with
        {
            AppMode = string.IsNullOrWhiteSpace(settings.AppMode) ? "Pilot" : settings.AppMode,
            ITSupportEmail = string.IsNullOrWhiteSpace(settings.ITSupportEmail) ? "it@zenhr.com" : settings.ITSupportEmail,
            CompanyName = string.IsNullOrWhiteSpace(settings.CompanyName) ? "ZenHR" : settings.CompanyName,
            UpdateChannel = NormalizeUpdateChannel(settings.UpdateChannel),
            Language = NormalizeLanguage(settings.Language),
            Theme = ThemeManager.Normalize(settings.Theme),
            LogRetentionDays = NormalizeRetentionDays(settings.LogRetentionDays, 30),
            ReportRetentionDays = NormalizeRetentionDays(settings.ReportRetentionDays, 14)
        };
    }

    private static string NormalizeUpdateChannel(string? updateChannel)
    {
        return updateChannel switch
        {
            "Production" or "Testing" or "Development" => updateChannel,
            _ => "Production"
        };
    }

    private static string NormalizeLanguage(string? language)
    {
        return language switch
        {
            "en" or "ar" => language,
            _ => "en"
        };
    }

    private static int NormalizeRetentionDays(int days, int fallback)
    {
        return days is >= 1 and <= 365 ? days : fallback;
    }

    private static bool ContainsLegacyProtectedPolicyFields(string json)
    {
        return json.Contains("\"ITModeUsername\"", StringComparison.OrdinalIgnoreCase) ||
               json.Contains("\"ITModePasswordHash\"", StringComparison.OrdinalIgnoreCase) ||
               json.Contains("\"AllowITCredentialChanges\"", StringComparison.OrdinalIgnoreCase) ||
               json.Contains("\"EnableITMode\"", StringComparison.OrdinalIgnoreCase);
    }
}
