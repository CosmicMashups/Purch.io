using Purch.Application.Auth;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Auth;

/// <summary>Stand-in for real password-reset email delivery: writes the raw token to
/// stdout (captured by whatever's hosting the process — Render/container logs locally)
/// so the reset flow is testable end-to-end before a real provider is wired in. Never
/// returned over HTTP — see PasswordResetService.RequestAsync for why. Outside development the token
/// is NOT written: logs are widely readable and a reset token is a password, so without a real
/// delivery provider a reset is simply not deliverable there.</summary>
public sealed class ConsolePasswordResetTokenNotifier(bool revealToken) : IPasswordResetTokenNotifier
{
    public Task NotifyAsync(User user, string rawToken, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(revealToken
            ? $"[PasswordReset] Reset token for user {user.Id} ({user.Email}): {rawToken}"
            : $"[PasswordReset] Reset requested for user {user.Id} but no email delivery is configured; the token was not delivered.");
        return Task.CompletedTask;
    }
}
