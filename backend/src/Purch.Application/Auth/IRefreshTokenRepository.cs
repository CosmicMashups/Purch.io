using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public interface IRefreshTokenRepository
{
    /// <summary>Tracked (not AsNoTracking) since a hit is immediately marked revoked
    /// on redemption. Deliberately not tenant-scoped in the query itself — like
    /// device pairing-code lookup, the caller doesn't know the tenant until this
    /// returns; the shared-database tenant filter still applies when a tenant is
    /// already known from an authenticated context.</summary>
    Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Every not-yet-revoked, not-yet-expired token issued to this user (staff/admin
    /// login) — for revoking every outstanding session on password change.</summary>
    Task<IReadOnlyList<RefreshToken>> ListActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Every not-yet-revoked, not-yet-expired token issued to this device (kiosk
    /// login) — for revoking every outstanding session when a device's pairing code/PIN is reset.</summary>
    Task<IReadOnlyList<RefreshToken>> ListActiveByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new refresh token for insert — call IUnitOfWork.SaveChangesAsync to commit.</summary>
    void Add(RefreshToken refreshToken);
}
