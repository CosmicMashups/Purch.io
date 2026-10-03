using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetActiveUsersByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>All staff for the current tenant, active or not — for the staff management screen (A4).</summary>
    Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>One of the few deliberately unscoped reads: used before a tenant is known (anonymous refresh
    /// and password reset), so it can't go through the tenant filter. Authenticated code must keep using GetByIdAsync.</summary>
    Task<User?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Looks up an admin/owner account by email for the email+password login
    /// path — deliberately not tenant-scoped, since the caller doesn't know which
    /// tenant they belong to until this returns (mirrors device pairing-code lookup).</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>A person by the id in their token: an older staff User, or a Membership shown as one (see
    /// <see cref="MembershipUserProjection"/>). For code that only reads who someone is. Never saved.</summary>
    Task<User?> FindActorAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Everyone in the business, older Users and Memberships alike, for names on reports. Never saved.</summary>
    Task<IReadOnlyList<User>> ListActorsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Active people who could approve something with a PIN. Deliberately not used by the old PIN sign-in, which
    /// must never match a Membership.</summary>
    Task<IReadOnlyList<User>> GetActiveActorsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new user for insert — call IUnitOfWork.SaveChangesAsync to commit.</summary>
    void Add(User user);
}
