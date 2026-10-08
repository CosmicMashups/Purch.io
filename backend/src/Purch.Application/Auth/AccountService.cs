using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public sealed record SignInRequest(string Email, string Password, Guid? TenantId = null);

public abstract record SignInResult
{
    public sealed record Success(string AccessToken, string RefreshToken) : SignInResult;

    /// <summary>The person belongs to several businesses and did not say which; the client asks, then signs in again with a tenant id.</summary>
    public sealed record ChooseBusiness(IReadOnlyList<BusinessChoice> Businesses) : SignInResult;

    /// <summary>Wrong email or password, no account, a deactivated account, or no business the person may sign in to. One case on purpose.</summary>
    public sealed record Invalid : SignInResult;
}

public interface IAccountService
{
    /// <summary>Creates the login at the identity provider and stages an Account for it (the caller saves). If the email
    /// already has an account, the password must match it: anyone may register a second business, but only the person who
    /// knows the password.</summary>
    Task<Account> CreateAccountAsync(string email, string displayName, string password, CancellationToken cancellationToken = default);

    /// <summary>Email and password sign-in for a personal device: no device, so no selling. Issues the API's own tokens.</summary>
    Task<SignInResult> SignInAsync(SignInRequest request, CancellationToken cancellationToken = default);
}

public sealed class AccountService(
    IIdentityProvider identityProvider,
    IAccountRepository accountRepository,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    ISignInThrottleRepository signInThrottle) : IAccountService
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<Account> CreateAccountAsync(string email, string displayName, string password, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@'))
        {
            throw new ValidationException(nameof(email), "Enter a valid email address.");
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ValidationException(nameof(password), "A password is required.");
        }

        var existing = await accountRepository.FindByEmailAsync(normalized, cancellationToken);
        if (existing is not null)
        {
            // Proving the password of an existing login is a password-guessing surface just like sign-in, so it
            // shares the same per-email backoff.
            var emailHash = SignInThrottlePolicy.HashEmail(normalized);
            var hadFailures = await RequireNotBlockedAsync(emailHash, cancellationToken);
            if (await identityProvider.VerifyPasswordAsync(normalized, password, cancellationToken) != existing.SupabaseUserId)
            {
                await signInThrottle.RecordFailureAsync(emailHash, cancellationToken);
                throw new ValidationException(nameof(email), "That email can't be used with the details given. If it's yours, use its existing password.");
            }

            if (hadFailures)
            {
                await signInThrottle.ClearAsync(emailHash, cancellationToken);
            }

            return existing;
        }

        var providerUserId = await identityProvider.CreateUserAsync(normalized, password, cancellationToken);
        var account = new Account
        {
            SupabaseUserId = providerUserId,
            Email = normalized,
            DisplayName = displayName.Trim(),
        };
        accountRepository.Add(account);
        return account;
    }

    public async Task<SignInResult> SignInAsync(SignInRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return new SignInResult.Invalid();
        }

        var email = NormalizeEmail(request.Email);

        // Throttled per email, known or not, so guessing a password is slowed however many addresses it comes from
        // and the throttle itself says nothing about whether an account exists.
        var emailHash = SignInThrottlePolicy.HashEmail(email);
        var hadFailures = await RequireNotBlockedAsync(emailHash, cancellationToken);

        // Always ask the provider, even for an unknown email, so a wrong password and an unknown account look the same.
        var providerUserId = await identityProvider.VerifyPasswordAsync(email, request.Password, cancellationToken);
        if (providerUserId is null)
        {
            await signInThrottle.RecordFailureAsync(emailHash, cancellationToken);
            return new SignInResult.Invalid();
        }

        if (hadFailures)
        {
            await signInThrottle.ClearAsync(emailHash, cancellationToken);
        }

        var account = await accountRepository.FindByProviderUserIdAsync(providerUserId.Value, cancellationToken);
        if (account is not { IsActive: true })
        {
            return new SignInResult.Invalid();
        }

        var memberships = (await accountRepository.ListActiveMembershipsAsync(account.Id, cancellationToken))
            .Where(m => MembershipRoleMapper.ToApiRole(m.Membership) is not null)
            .ToList();
        if (memberships.Count == 0)
        {
            return new SignInResult.Invalid();
        }

        (Membership Membership, string TenantName) chosen;
        if (request.TenantId is { } tenantId)
        {
            var match = memberships.Where(m => m.Membership.TenantId == tenantId).ToList();
            if (match.Count == 0)
            {
                return new SignInResult.Invalid();
            }

            chosen = match[0];
        }
        else if (memberships.Count == 1)
        {
            chosen = memberships[0];
        }
        else
        {
            return new SignInResult.ChooseBusiness(memberships.Select(m => new BusinessChoice(m.Membership.TenantId, m.TenantName)).ToList());
        }

        var accessToken = jwtTokenService.IssueMembershipAccessToken(chosen.Membership);
        var refreshToken = await refreshTokenService.IssueForMembershipAsync(chosen.Membership.TenantId, chosen.Membership.Id, null, cancellationToken);
        return new SignInResult.Success(accessToken, refreshToken);
    }

    /// <summary>Throws if attempts for this email are blocked right now; otherwise says whether any failures are on record.</summary>
    private async Task<bool> RequireNotBlockedAsync(string emailHash, CancellationToken cancellationToken)
    {
        var state = await signInThrottle.GetAsync(emailHash, cancellationToken);
        if (state?.BlockedUntil is { } until && until > DateTimeOffset.UtcNow)
        {
            throw new TooManyRequestsException("Too many sign-in attempts. Wait a little and try again.", until - DateTimeOffset.UtcNow);
        }

        return state is not null;
    }
}
