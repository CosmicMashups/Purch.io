using Purch.Domain.Entities;

namespace Purch.Application.Auth;

/// <summary>One business an account belongs to, as shown when a person has to pick which one to sign in to.</summary>
public sealed record BusinessChoice(Guid TenantId, string Name);

/// <summary>Accounts are not tenant-scoped, and the lookups here run before a business is known, so none of them
/// apply the tenant filter.</summary>
public interface IAccountRepository
{
    Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<Account?> FindByProviderUserIdAsync(Guid providerUserId, CancellationToken cancellationToken = default);

    /// <summary>Active memberships of this account, in businesses that still exist, with the branches loaded.</summary>
    Task<IReadOnlyList<(Membership Membership, string TenantName)>> ListActiveMembershipsAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<Membership?> GetMembershipAsync(Guid membershipId, CancellationToken cancellationToken = default);

    void Add(Account account);

    void Add(Membership membership);
}
