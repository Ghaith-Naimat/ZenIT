namespace ZenIT.Core.Models;

public sealed record DeviceHealthInfo(
    string DeviceName,
    string CurrentWindowsUsername,
    string WindowsVersion,
    TimeSpan Uptime,
    string InternetConnectivityStatus,
    long FreeDiskSpaceBytes,
    long TotalDiskSpaceBytes,
    int? BatteryPercentage,
    DateTimeOffset CurrentLocalTime);
