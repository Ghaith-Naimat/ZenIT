using ZenIT.Core.Configuration;
using ZenIT.Core.Execution;

namespace ZenIT.Mac.Tests;

public sealed class CommandAllowlistTests
{
    [Theory]
    [InlineData("diskutil", "eraseDisk APFS Wiped /dev/disk0")]
    [InlineData("softwareupdate", "-i -a")]
    [InlineData("killall", "coreaudiod")]
    [InlineData("networksetup", "-setairportpower en0 off")]
    [InlineData("rm", "-rf /")]
    [InlineData("osascript", "-e 'do shell script \"whoami\"'")]
    public async Task EmployeeProcessRunner_RejectsPrivilegedCommands(string fileName, string arguments)
    {
        var runner = new ProcessRunner();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(fileName, arguments, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task EmployeeProcessRunner_RejectsUnknownDscacheutilArguments()
    {
        var runner = new ProcessRunner();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync("dscacheutil", "-q user", TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task EmployeeProcessRunner_RejectsOpeningArbitraryFolders()
    {
        var runner = new ProcessRunner();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync("open", "/Users", TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void ContactITUrl_IsLockedToStandardSlackUrl()
    {
        Assert.Equal("https://zenhr.slack.com/team/U09CGMUGV6K", ITPolicy.DefaultContactITUrl);
    }
}
