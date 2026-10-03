using Purch.Domain.Entities;

namespace Purch.Application.Auth;

/// <summary>Looks people up as a <see cref="User"/> for code that still reads who someone is: a person's sales, shifts and
/// approvals carry the id from their token. People are memberships now; one is shown as a User built on the fly (see
/// <see cref="MembershipUserProjection"/>), and older sales and shifts still resolve to the original User rows, which stay for
/// history. Nothing here writes.</summary>
public interface IUserRepository
{
    /// <summary>A person by the id in their token or on an older record. Never saved.</summary>
    Task<User?> FindActorAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Everyone in the business, for names on reports. Never saved.</summary>
    Task<IReadOnlyList<User>> ListActorsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Active people who could approve something with a PIN.</summary>
    Task<IReadOnlyList<User>> GetActiveActorsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Older Users who have not yet been invited to the new sign-in, so the Staff page can offer to invite them.</summary>
    Task<IReadOnlyList<User>> ListUninvitedLegacyAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
