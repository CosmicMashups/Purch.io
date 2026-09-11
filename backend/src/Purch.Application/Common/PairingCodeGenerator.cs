using System.Security.Cryptography;

namespace Purch.Application.Common;

/// <summary>
/// Generates a short, human-typeable device pairing code. Uniqueness is
/// enforced at the database level (a unique index on Device.PairingCode,
/// global across tenants — a device pairing code names one specific device,
/// so it must be findable without already knowing which tenant it belongs
/// to, per the login flow). The vanishingly small chance of a collision
/// surfaces as a 409 Conflict via GlobalExceptionHandler's DbUpdateException
/// mapping, not a crash.
/// </summary>
public static class PairingCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I — avoids misreads

    public static string Generate(int length = 8)
    {
        Span<byte> randomBytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        return string.Create(length, randomBytes.ToArray(), (span, bytes) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Alphabet[bytes[i] % Alphabet.Length];
            }
        });
    }
}
