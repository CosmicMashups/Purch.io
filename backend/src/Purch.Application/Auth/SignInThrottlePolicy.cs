using System.Security.Cryptography;
using System.Text;

namespace Purch.Application.Auth;

/// <summary>Backoff for password guessing against one email: five misses in a quarter of an hour start a block that
/// doubles from one minute to a cap of fifteen. A block is short and ends by itself, so a stranger typing wrong
/// passwords for someone's email can slow the owner down but never lock them out for good.</summary>
public static class SignInThrottlePolicy
{
    public const int FreeAttempts = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxBlock = TimeSpan.FromMinutes(15);

    /// <summary>How long to refuse attempts after this many failures in a row; zero while attempts are still free.</summary>
    public static TimeSpan BlockFor(int failures)
    {
        if (failures < FreeAttempts)
        {
            return TimeSpan.Zero;
        }

        var minutes = Math.Pow(2, Math.Min(failures - FreeAttempts, 10));
        return TimeSpan.FromMinutes(Math.Min(minutes, MaxBlock.TotalMinutes));
    }

    public static string HashEmail(string normalizedEmail) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));
}
