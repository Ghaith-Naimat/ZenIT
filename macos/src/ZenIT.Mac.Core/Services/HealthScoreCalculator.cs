using ZenIT.Core.Models;

namespace ZenIT.Core.Services;

public static class HealthScoreCalculator
{
    public static HealthScoreResult Calculate(HealthScoreInput input)
    {
        var score = 100;

        if (!input.InternetOnline)
        {
            score -= 25;
        }

        if (!input.GatewayAvailable)
        {
            score -= 10;
        }

        if (!input.DnsAvailable)
        {
            score -= 10;
        }

        if (!input.DiskHealthy)
        {
            score -= 20;
        }

        if (!input.MemoryHealthy)
        {
            score -= 10;
        }

        if (!input.CpuHealthy)
        {
            score -= 10;
        }

        if (!input.BatteryHealthy)
        {
            score -= 5;
        }

        if (input.PendingReboot)
        {
            score -= 10;
        }

        if (!input.JumpCloudDetected)
        {
            score -= 15;
        }

        if (!input.GoogleDriveRunning)
        {
            score -= 5;
        }

        if (input.CriticalServiceFailure)
        {
            score -= 20;
        }

        score = Math.Clamp(score, 0, 100);
        return new HealthScoreResult(score, GetStatus(score), GetBrush(score));
    }

    private static string GetStatus(int score)
    {
        return score switch
        {
            >= 95 => "Excellent",
            >= 80 => "Good",
            >= 60 => "Needs Attention",
            _ => "Contact IT"
        };
    }

    private static string GetBrush(int score)
    {
        return score switch
        {
            >= 80 => "#20B486",
            >= 60 => "#F5A623",
            _ => "#E5484D"
        };
    }
}

public sealed record HealthScoreResult(int Score, string Status, string Brush);

