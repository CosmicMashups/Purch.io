using System.Security.Cryptography;
using System.Text;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public sealed class PasswordResetService(
    IUserRepository userRepository,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordHasher passwordHasher,
    IRefreshTokenService refreshTokenService,
    IPasswordResetTokenNotifier tokenNotifier,
    IUnitOfWork unitOfWork) : IPasswordResetService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public async Task RequestAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException(nameof(email), "Email is required.");
        }

        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

        // Only an active admin/owner account with email+password login enabled can have
        // its password reset — silently no-ops for anything else so this endpoint never
        // reveals which emails have an account.
        if (user is null || !user.IsActive || user.PasswordHash is null)
        {
            return;
        }

        // Only the newest link should work: an older email sitting in an inbox (or a mail log) must
        // not stay usable just because a newer one was requested.
        var now = DateTimeOffset.UtcNow;
        foreach (var outstanding in await passwordResetTokenRepository.ListOutstandingByUserAsync(user.Id, cancellationToken))
        {
            outstanding.UsedAt = now;
        }

        var rawToken = GenerateRawToken();
        passwordResetTokenRepository.Add(new PasswordResetToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime),
        });

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        await tokenNotifier.NotifyAsync(user, rawToken, cancellationToken);
    }

    public async Task<PasswordResetResult> ConfirmAsync(string rawToken, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return new PasswordResetResult.InvalidToken();
        }

        if (PasswordPolicy.Validate(newPassword) is { } passwordError)
        {
            throw new ValidationException(nameof(newPassword), passwordError);
        }

        var existing = await passwordResetTokenRepository.FindByTokenHashAsync(Hash(rawToken), cancellationToken);
        if (existing is null || existing.UsedAt is not null || existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return new PasswordResetResult.InvalidToken();
        }

        var user = await userRepository.GetByIdAsync(existing.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"PasswordResetToken {existing.Id} references a missing user {existing.UserId}.");

        user.PasswordHash = passwordHasher.Hash(newPassword);
        existing.UsedAt = DateTimeOffset.UtcNow;

        // Closes out every session issued before the change so a stolen-but-still-valid refresh token
        // can't keep renewing itself past the reset. Staged, not saved separately, so the new password
        // and the end of the old sessions commit together or not at all.
        await refreshTokenService.StageRevokeAllForUserAsync(user.Id, cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return new PasswordResetResult.Success();
    }

    private static string GenerateRawToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string Hash(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
