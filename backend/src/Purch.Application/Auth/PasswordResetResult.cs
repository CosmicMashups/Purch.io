namespace Purch.Application.Auth;

public abstract record PasswordResetResult
{
    public sealed record Success : PasswordResetResult;

    /// <summary>Covers missing, expired, and already-redeemed tokens alike — deliberately
    /// undifferentiated, same reasoning as LoginResult.InvalidDevice/InvalidPin.</summary>
    public sealed record InvalidToken : PasswordResetResult;
}
