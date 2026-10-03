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
    IRefreshTokenService refreshTokenService) : IAccountService
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
            if (await identityProvider.VerifyPasswordAsync(normalized, password, cancellationToken) != existing.SupabaseUserId)
            {
                throw new ValidationException(nameof(email), "That email already has an account. Use its password to add another business.");
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

        // Always ask the provider, even for an unknown email, so a wrong password and an unknown account look the same.
        var providerUserId = await identityProvider.VerifyPasswordAsync(email, request.Password, cancellationToken);
        if (providerUserId is null)
        {
            return new SignInResult.Invalid();
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
        var refreshToken = await refreshTokenService.IssueForMembershipAsync(chosen.Membership.TenantId, chosen.Membership.Id, cancellationToken);
        return new SignInResult.Success(accessToken, refreshToken);
    }
}
