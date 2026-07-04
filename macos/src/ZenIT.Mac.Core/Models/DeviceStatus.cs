namespace ZenIT.Core.Models;

public sealed record DeviceStatus(
    string DeviceName,
    string CurrentUser,
    string InternetStatus,
    DateTimeOffset LastCheckTime);
