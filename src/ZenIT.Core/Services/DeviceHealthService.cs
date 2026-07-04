using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using ZenIT.Core.Models;

namespace ZenIT.Core.Services;

public sealed class DeviceHealthService
{
    public DeviceHealthInfo GetCurrentHealth()
    {
        var systemDrive = new DriveInfo(@"C:\");
        var batteryPercentage = TryGetBatteryPercentage();

        return new DeviceHealthInfo(
            Environment.MachineName,
            Environment.UserName,
            Environment.OSVersion.VersionString,
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            NetworkInterface.GetIsNetworkAvailable() ? "Connected" : "Offline",
            systemDrive.AvailableFreeSpace,
            systemDrive.TotalSize,
            batteryPercentage,
            DateTimeOffset.Now);
    }

    private static int? TryGetBatteryPercentage()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        if (!GetSystemPowerStatus(out var status))
        {
            return null;
        }

        return status.BatteryLifePercent > 100 ? null : status.BatteryLifePercent;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}
