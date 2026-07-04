using ZenIT.Core.Localization;

namespace ZenIT.Mac.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void EnglishAndArabicKeys_HaveParityAndNoEmptyValues()
    {
        var english = LocalizedStrings.Resources["en"];
        var arabic = LocalizedStrings.Resources["ar"];

        Assert.Equal(english.Keys.OrderBy(key => key), arabic.Keys.OrderBy(key => key));
        Assert.All(english, item => Assert.False(string.IsNullOrWhiteSpace(item.Value), item.Key));
        Assert.All(arabic, item => Assert.False(string.IsNullOrWhiteSpace(item.Value), item.Key));
    }

    [Theory]
    [InlineData("Label.DNS")]
    [InlineData("Device.HealthScoreTooltip")]
    [InlineData("HealthScore.Excellent")]
    [InlineData("HealthScore.Good")]
    [InlineData("HealthScore.NeedsAttention")]
    [InlineData("HealthScore.ContactIT")]
    [InlineData("Message.DeviceOptimizationComplete")]
    [InlineData("Message.DeviceOptimizationNoMajorIssues")]
    [InlineData("Message.OptionalAppRefreshSkipped")]
    [InlineData("Message.SomeIssuesNeedIT")]
    [InlineData("About.Title")]
    [InlineData("IT.AdministratorAccess")]
    [InlineData("IT.AdministratorAccessSubtitle")]
    [InlineData("IT.Authenticating")]
    [InlineData("IT.AdministratorVerified")]
    [InlineData("IT.SignedInAs")]
    [InlineData("IT.LockedInactivity")]
    [InlineData("IT.NeedAccess")]
    [InlineData("IT.CredentialsProtected")]
    [InlineData("IT.Invalid")]
    [InlineData("IT.PolicyUnavailable")]
    [InlineData("IT.PolicyUnavailableInstall")]
    public void ImportantKeys_AreLocalized(string key)
    {
        Assert.NotEqual(key, LocalizedStrings.Get("en", key));
        Assert.NotEqual(key, LocalizedStrings.Get("ar", key));
    }
}
