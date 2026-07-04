using ZenIT.Core.Configuration;
using ZenIT.Core.Execution;

namespace ZenIT.Tests;

public sealed class CommandAllowlistTests
{
    [Theory]
    [InlineData("netsh", "winsock reset")]
    [InlineData("netsh", "int ip reset")]
    [InlineData("sfc", "/scannow")]
    [InlineData("DISM", "/Online /Cleanup-Image /RestoreHealth")]
    [InlineData("chkdsk", "/f")]
    public async Task EmployeeProcessRunner_RejectsPrivilegedCommands(string fileName, string arguments)
    {
        var runner = new ProcessRunner();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(fileName, arguments, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void ContactITUrl_IsLockedToStandardSlackUrl()
    {
        Assert.Equal("https://zenhr.slack.com/team/U09CGMUGV6K", ITPolicy.DefaultContactITUrl);
    }
}
