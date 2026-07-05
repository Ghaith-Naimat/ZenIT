using System.Text.Json;

namespace ZenIT.Core.Configuration;

public sealed class ITPolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _policyPath;
    private readonly ZenITPathProvider _paths;

    public ITPolicyService(string? policyPath = null, ZenITPathProvider? paths = null)
    {
        _paths = paths ?? ZenITPathProvider.CreateProduction();
        _policyPath = policyPath ?? _paths.ITPolicyPath;
    }

    public ITPolicy Load()
    {
        try
        {
            if (!File.Exists(_policyPath))
            {
                return ITPolicy.Disabled;
            }

            var policy = JsonSerializer.Deserialize<ITPolicy>(File.ReadAllText(_policyPath), JsonOptions);
            return Normalize(policy ?? ITPolicy.Disabled);
        }
        catch
        {
            return ITPolicy.Disabled;
        }
    }

    public ITPolicy LoadOrCreate()
    {
        Directory.CreateDirectory(_paths.PolicyDirectory);
        if (!File.Exists(_policyPath))
        {
            var defaults = new ITPolicy();
            Save(defaults);
            return defaults;
        }

        var policy = Load();
        var normalized = Normalize(policy);
        if (!normalized.Equals(policy))
        {
            Save(normalized);
        }

        return normalized;
    }

    public void Save(ITPolicy policy)
    {
        Directory.CreateDirectory(_paths.PolicyDirectory);
        AtomicJsonFile.Write(_policyPath, Normalize(policy), JsonOptions);
    }

    public static ITPolicy Normalize(ITPolicy policy)
    {
        if (!policy.EnableITMode)
        {
            return policy with
            {
                AllowITCredentialChanges = false,
                ITModeSessionTimeoutMinutes = NormalizeTimeout(policy.ITModeSessionTimeoutMinutes),
                ContactITUrl = NormalizeContactUrl(policy.ContactITUrl)
            };
        }

        return policy with
        {
            EnableITMode = true,
            ITModeUsername = ITPolicy.DefaultITModeUsername,
            ITModePasswordHash = ITPolicy.DefaultITModePasswordHash,
            AllowITCredentialChanges = false,
            ITModeSessionTimeoutMinutes = NormalizeTimeout(policy.ITModeSessionTimeoutMinutes),
            ContactITUrl = NormalizeContactUrl(policy.ContactITUrl)
        };
    }

    private static int NormalizeTimeout(int timeoutMinutes)
    {
        return timeoutMinutes is >= 5 and <= 240 ? timeoutMinutes : 15;
    }

    private static string NormalizeContactUrl(string? contactUrl)
    {
        return Uri.TryCreate(contactUrl, UriKind.Absolute, out var uri) &&
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? uri.ToString()
            : ITPolicy.DefaultContactITUrl;
    }
}
