namespace ZenIT.Core.Models;

public sealed record HealthScoreInput(
    bool InternetOnline,
    bool GatewayAvailable,
    bool DnsAvailable,
    bool DiskHealthy,
    bool MemoryHealthy,
    bool CpuHealthy,
    bool BatteryHealthy,
    bool PendingReboot,
    bool JumpCloudDetected,
    bool GoogleDriveRunning,
    bool CriticalServiceFailure = false);

