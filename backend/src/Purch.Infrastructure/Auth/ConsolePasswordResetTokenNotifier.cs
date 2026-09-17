using Purch.Application.Auth;
using Purch.Domain.Entities;

namespace Purch.Infrastructure.Auth;

/// <summary>Stand-in for real password-reset email delivery: writes the raw token to
/// stdout (captured by whatever's hosting the process — Render/container logs locally)
/// so the reset flow is testable end-to-end before a real provider is wired in. Never
/// returned over HTTP — see PasswordResetService.RequestAsync for why.</summary>
public sealed class ConsolePasswordResetTokenNotifier : IPasswordResetTokenNotifier
{
    public Task NotifyAsync(User user, string rawToken, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[PasswordReset] Reset token for user {user.Id} ({user.Email}): {rawToken}");
        return Task.CompletedTask;
    }
}
