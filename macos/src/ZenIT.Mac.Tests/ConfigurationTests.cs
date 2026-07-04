using System.Text.Json;
using ZenIT.Core.Configuration;
using ZenIT.Core.Security;

namespace ZenIT.Mac.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void AppSettings_NormalizesInvalidLanguageAndTheme()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"ZenIT-settings-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, JsonSerializer.Serialize(new AppSettings
        {
            Language = "fr",
            Theme = "Solarized",
            LogRetentionDays = 999,
            ReportRetentionDays = 0
        }));

        var settings = new AppSettingsService(tempPath).LoadOrCreate();

        Assert.Equal("en", settings.Language);
        Assert.Equal("Dark", settings.Theme);
        Assert.Equal(30, settings.LogRetentionDays);
        Assert.Equal(14, settings.ReportRetentionDays);
        Assert.DoesNotContain("ITModePasswordHash", File.ReadAllText(tempPath));
    }

    [Fact]
    public void ProtectedPolicy_NormalizesAndOverridesSensitiveValues()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"ZenIT-policy-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, JsonSerializer.Serialize(new ITPolicy
        {
            EnableITMode = true,
            ITModeUsername = "UserTamper",
            ITModePasswordHash = "BAD",
            AllowITCredentialChanges = true,
            ContactITUrl = "http://unsafe.example"
        }));

        var policy = new ITPolicyService(tempPath).LoadOrCreate();

        Assert.True(policy.EnableITMode);
        Assert.Equal(ITPolicy.DefaultITModeUsername, policy.ITModeUsername);
        Assert.Equal(ITPolicy.DefaultITModePasswordHash, policy.ITModePasswordHash);
        Assert.False(policy.AllowITCredentialChanges);
        Assert.Equal(ITPolicy.DefaultContactITUrl, policy.ContactITUrl);
        Assert.Equal(15, policy.ITModeSessionTimeoutMinutes);
    }

    [Fact]
    public void ProtectedPolicy_DefaultCredentialsAreHashedAndLocked()
    {
        var policy = new ITPolicy();
        var configuredPassword = new string(['Z', 'e', 'n', 'H', 'R', 'I', 'T']);

        Assert.True(policy.EnableITMode);
        Assert.Equal("Ghaith", policy.ITModeUsername);
        Assert.Equal(PasswordHashService.HashPassword(configuredPassword), policy.ITModePasswordHash);
        Assert.True(PasswordHashService.VerifyPassword(configuredPassword, policy.ITModePasswordHash));
        Assert.False(PasswordHashService.VerifyPassword("wrong-password", policy.ITModePasswordHash));
        Assert.False(policy.AllowITCredentialChanges);
        Assert.Equal(15, policy.ITModeSessionTimeoutMinutes);
        Assert.DoesNotContain(configuredPassword, JsonSerializer.Serialize(policy));
    }

    [Fact]
    public void ProtectedPolicy_NormalizesInvalidTimeout()
    {
        var normalizedLow = ITPolicyService.Normalize(new ITPolicy { ITModeSessionTimeoutMinutes = 1 });
        var normalizedHigh = ITPolicyService.Normalize(new ITPolicy { ITModeSessionTimeoutMinutes = 999 });
        var accepted = ITPolicyService.Normalize(new ITPolicy { ITModeSessionTimeoutMinutes = 30 });

        Assert.Equal(15, normalizedLow.ITModeSessionTimeoutMinutes);
        Assert.Equal(15, normalizedHigh.ITModeSessionTimeoutMinutes);
        Assert.Equal(30, accepted.ITModeSessionTimeoutMinutes);
    }
}
