namespace ZenIT.Core.Services;

public sealed record ApplicationProcessProfile(
    string DisplayName,
    IReadOnlyCollection<string> ProcessNames,
    IReadOnlyCollection<string> ExecutableCandidates);

