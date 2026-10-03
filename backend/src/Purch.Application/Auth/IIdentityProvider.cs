namespace Purch.Application.Auth;

/// <summary>The part of sign-in that checks an email and password. In Cloud mode this is Supabase Auth; in Local mode (and
/// tests) it is a table in our own database. Who the person is allowed to be, and where, never comes from here: that lives
/// on <see cref="Purch.Domain.Entities.Membership"/>.</summary>
public interface IIdentityProvider
{
    /// <summary>Creates a login for this email, already confirmed (no verification email is ever sent). Throws a
    /// ConflictException when the email already has one.</summary>
    Task<Guid> CreateUserAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>The provider's user id when the email and password match, otherwise null. Never says which part was wrong.</summary>
    Task<Guid?> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default);

    Task SetPasswordAsync(Guid providerUserId, string newPassword, CancellationToken cancellationToken = default);
}
