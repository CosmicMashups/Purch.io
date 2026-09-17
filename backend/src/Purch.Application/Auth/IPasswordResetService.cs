namespace Purch.Application.Auth;

public interface IPasswordResetService
{
    /// <summary>Always completes the same way whether or not the email belongs to an
    /// account — never reveals which emails exist (see LoginService.AdminLoginAsync for
    /// the same pattern on login).</summary>
    Task RequestAsync(string email, CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ConfirmAsync(string rawToken, string newPassword, CancellationToken cancellationToken = default);
}
