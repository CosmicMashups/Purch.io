using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public interface IPasswordResetTokenRepository
{
    /// <summary>Tracked (not AsNoTracking) since a hit is immediately marked used on
    /// redemption. Not tenant-scoped in the query itself — like refresh-token lookup,
    /// the caller doesn't know the tenant until this returns.</summary>
    Task<PasswordResetToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>The user's reset tokens that are neither used nor expired, tracked so they can be retired.</summary>
    Task<IReadOnlyList<PasswordResetToken>> ListOutstandingByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new password reset token for insert — call IUnitOfWork.SaveChangesAsync to commit.</summary>
    void Add(PasswordResetToken token);
}
