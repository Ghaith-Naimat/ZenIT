using ZenIT.Core.Models;

namespace ZenIT.Core.Services;

public interface IDeviceStatusProvider
{
    DeviceStatus GetCurrentStatus();
}
