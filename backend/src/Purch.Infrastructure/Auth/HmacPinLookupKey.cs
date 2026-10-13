using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Purch.Application.Auth;

namespace Purch.Infrastructure.Auth;

/// <summary>HMAC-SHA256 over "tenant:pin" with a secret key, hex encoded. The key comes from PIN_LOOKUP_KEY when it is
/// set, otherwise it is derived (HKDF, with a purpose label) from JWT_SIGNING_KEY. Without the key a stolen database
/// cannot be used to test PIN guesses against these values.</summary>
public sealed class HmacPinLookupKey(IConfiguration configuration) : IPinLookupKey
{
    private static readonly byte[] KeyPurpose = "purch.pin-lookup.v1"u8.ToArray();

    private readonly Lazy<byte[]> key = new(() =>
    {
        var secret = configuration["PIN_LOOKUP_KEY"] ?? configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("PIN_LOOKUP_KEY or JWT_SIGNING_KEY must be configured.");
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret), 32, salt: null, info: KeyPurpose);
    });

    public string Compute(Guid tenantId, string pin)
    {
        var data = Encoding.UTF8.GetBytes($"{tenantId:N}:{pin}");
        return Convert.ToHexString(HMACSHA256.HashData(key.Value, data)).ToLowerInvariant();
    }
}
