using Purch.Domain.Entities;

namespace Purch.Application.Auth;

/// <summary>Delivers a freshly issued password-reset token to its owner. No real
/// email/notification provider exists in this codebase yet — see the console-logging
/// implementation in Purch.Infrastructure for the current (dev-only) behavior. Kept as
/// its own port so a real provider (SMTP/SendGrid/etc.) can be swapped in later without
/// touching PasswordResetService.</summary>
public interface IPasswordResetTokenNotifier
{
    Task NotifyAsync(User user, string rawToken, CancellationToken cancellationToken = default);
}
