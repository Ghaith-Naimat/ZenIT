using ZenIT.Core.Models;
using ZenIT.Core.Services;

namespace ZenIT.Mac.Tests;

public sealed class HealthScoreCalculatorTests
{
    [Fact]
    public void AllHealthyRequiredComponents_ReturnsOneHundred()
    {
        var result = HealthScoreCalculator.Calculate(HealthyInput());

        Assert.Equal(100, result.Score);
        Assert.Equal("Excellent", result.Status);
    }

    [Fact]
    public void ReviewOnlyOptionalRows_DoNotReduceScore()
    {
        var result = HealthScoreCalculator.Calculate(HealthyInput());

        Assert.Equal(100, result.Score);
    }

    [Fact]
    public void VpnDetected_DoesNotReduceScore()
    {
        var result = HealthScoreCalculator.Calculate(HealthyInput());

        Assert.Equal(100, result.Score);
    }

    [Fact]
    public void StartupImpactNotCollected_DoesNotReduceScore()
    {
        var result = HealthScoreCalculator.Calculate(HealthyInput());

        Assert.Equal(100, result.Score);
    }

    [Fact]
    public void LowDisk_ReducesScore()
    {
        var result = HealthScoreCalculator.Calculate(HealthyInput(diskHealthy: false));

        Assert.True(result.Score < 100);
    }

    [Fact]
    public void InternetOffline_ReducesScore()
    {
        var result = HealthScoreCalculator.Calculate(HealthyInput(internetOnline: false));

        Assert.True(result.Score < 100);
    }

    [Fact]
    public void PendingReboot_ReducesScore()
    {
        var result = HealthScoreCalculator.Calculate(HealthyInput(pendingReboot: true));

        Assert.True(result.Score < 100);
    }

    [Fact]
    public void JumpCloudMissing_ReducesScore()
    {
        var result = HealthScoreCalculator.Calculate(HealthyInput(jumpCloudDetected: false));

        Assert.True(result.Score < 100);
    }

    private static HealthScoreInput HealthyInput(
        bool internetOnline = true,
        bool gatewayAvailable = true,
        bool dnsAvailable = true,
        bool diskHealthy = true,
        bool memoryHealthy = true,
        bool cpuHealthy = true,
        bool batteryHealthy = true,
        bool pendingReboot = false,
        bool jumpCloudDetected = true,
        bool googleDriveRunning = true)
    {
        return new HealthScoreInput(
            internetOnline,
            gatewayAvailable,
            dnsAvailable,
            diskHealthy,
            memoryHealthy,
            cpuHealthy,
            batteryHealthy,
            pendingReboot,
            jumpCloudDetected,
            googleDriveRunning);
    }
}

