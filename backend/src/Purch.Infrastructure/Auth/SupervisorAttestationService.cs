using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Purch.Application.Auth;

namespace Purch.Infrastructure.Auth;

/// <summary>Attestations are <c>v1.&lt;payload&gt;.&lt;signature&gt;</c>: a small JSON payload and an HMAC-SHA256 of it. The HMAC key is
/// derived (HKDF) from the JWT signing key with a purpose label, so a signature made for one purpose can never be replayed as
/// another and the raw JWT key is not used directly.</summary>
public sealed class SupervisorAttestationService(IConfiguration configuration) : ISupervisorAttestationService
{
    private const string Version = "v1";
    private static readonly byte[] KeyPurpose = "purch.supervisor-attestation.v1"u8.ToArray();

    private sealed record Payload(Guid M, Guid T, Guid D, long Iat, long Exp);

    public SupervisorAttestation Issue(Guid supervisorMembershipId, Guid tenantId, Guid deviceId, DateTimeOffset now)
    {
        var expires = now + ISupervisorAttestationService.Lifetime;
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new Payload(supervisorMembershipId, tenantId, deviceId, now.ToUnixTimeSeconds(), expires.ToUnixTimeSeconds()));
        var encoded = Base64Url(payload);
        return new SupervisorAttestation($"{Version}.{encoded}.{Base64Url(Sign(encoded))}", expires);
    }

    public Guid? Validate(string token, Guid tenantId, Guid deviceId, DateTimeOffset saleTime)
    {
        var parts = (token ?? string.Empty).Split('.');
        if (parts.Length != 3 || parts[0] != Version)
        {
            return null;
        }

        byte[] signature;
        byte[] payloadBytes;
        try
        {
            signature = FromBase64Url(parts[2]);
            payloadBytes = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        // Constant-time, so the signature cannot be guessed a byte at a time from response timing.
        if (!CryptographicOperations.FixedTimeEquals(signature, Sign(parts[1])))
        {
            return null;
        }

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(payloadBytes);
        }
        catch (JsonException)
        {
            return null;
        }

        var at = saleTime.ToUnixTimeSeconds();
        return payload is not null && payload.T == tenantId && payload.D == deviceId && payload.Iat <= at && at <= payload.Exp
            ? payload.M
            : null;
    }

    private byte[] Sign(string encodedPayload)
    {
        var jwtKey = configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwtKey), 32, salt: null, info: KeyPurpose);
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(encodedPayload));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
