using System.Security.Cryptography;

namespace EventTracker.Data;

public static class Tokens
{
    /// <summary>32 lower-case hex characters from a cryptographic random source.</summary>
    public static string Create() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}
