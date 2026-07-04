using System.Net.NetworkInformation;
using ZenIT.Core.Models;

namespace ZenIT.Core.Services;

public sealed class DeviceHealthService
{
    public DeviceHealthInfo GetCurrentHealth()
    {
        var systemDrive = new DriveInfo("/");
        var batteryPercentage = MacSystemInfo.GetBatteryPercentage();

        return new DeviceHealthInfo(
            Environment.MachineName,
            Environment.UserName,
            MacSystemInfo.GetMacOSVersionString(),
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            NetworkInterface.GetIsNetworkAvailable() ? "Connected" : "Offline",
            systemDrive.AvailableFreeSpace,
            systemDrive.TotalSize,
            batteryPercentage,
            DateTimeOffset.Now);
    }
}
