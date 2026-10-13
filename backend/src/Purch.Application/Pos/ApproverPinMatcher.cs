using Purch.Application.Auth;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;

namespace Purch.Application.Pos;

public interface IApproverPinMatcher
{
    /// <summary>The candidate whose PIN this is, or null. Candidates whose stored PIN fingerprint matches are checked
    /// first (normally exactly one BCrypt check). Candidates with no fingerprint yet (older accounts) are then
    /// checked one by one, and a person who matches gets their fingerprint saved with the caller's next save.
    /// A candidate whose fingerprint is for a different PIN can not match and is never checked.</summary>
    Task<User?> FindAsync(IReadOnlyList<User> candidates, string pin, Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class ApproverPinMatcher(
    IPinHasher pinHasher,
    IPinLookupKey pinLookupKey,
    IMembershipRepository membershipRepository) : IApproverPinMatcher
{
    public async Task<User?> FindAsync(IReadOnlyList<User> candidates, string pin, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var lookup = pinLookupKey.Compute(tenantId, pin);

        foreach (var user in candidates)
        {
            if (user.PinLookup == lookup && HasPin(user) && pinHasher.Verify(pin, user.PinHash))
            {
                return user;
            }
        }

        foreach (var user in candidates)
        {
            if (string.IsNullOrEmpty(user.PinLookup) && HasPin(user) && pinHasher.Verify(pin, user.PinHash))
            {
                // Remember it so this person is found by lookup from now on. Legacy users have no membership row.
                var membership = await membershipRepository.GetByIdAsync(user.Id, cancellationToken);
                if (membership is not null && string.IsNullOrEmpty(membership.PinLookup))
                {
                    membership.PinLookup = lookup;
                }

                return user;
            }
        }

        return null;
    }

    private static bool HasPin(User user) => !string.IsNullOrEmpty(user.PinHash);
}
