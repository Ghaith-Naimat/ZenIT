namespace ZenIT.Core.Execution;

public sealed record ProcessRunResult(
    string FileName,
    string Arguments,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt);
