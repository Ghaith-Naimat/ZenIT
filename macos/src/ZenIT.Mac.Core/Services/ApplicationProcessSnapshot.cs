namespace ZenIT.Core.Services;

public sealed record ApplicationProcessSnapshot(
    bool IsRunning,
    bool HasVisibleWindow,
    bool HasHiddenProcesses,
    bool HasUnresponsiveProcesses,
    int ProcessCount,
    string TechnicalMessage);

