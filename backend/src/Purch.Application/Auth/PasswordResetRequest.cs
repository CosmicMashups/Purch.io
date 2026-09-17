namespace Purch.Application.Auth;

public sealed record PasswordResetRequest(string Email);

public sealed record PasswordResetConfirmRequest(string Token, string NewPassword);
