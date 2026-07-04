using System.Security.Cryptography;
using System.Text;

namespace ZenIT.Core.Security;

public static class PasswordHashService
{
    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public static bool VerifyPassword(string password, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        var actualHash = HashPassword(password);
        var normalizedExpectedHash = expectedHash.Trim().ToUpperInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actualHash),
            Encoding.ASCII.GetBytes(normalizedExpectedHash));
    }
}
