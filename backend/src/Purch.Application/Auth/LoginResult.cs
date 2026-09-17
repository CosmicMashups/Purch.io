namespace Purch.Application.Auth;

public abstract record LoginResult
{
    public sealed record Success(string AccessToken, string RefreshToken) : LoginResult;

    public sealed record InvalidDevice : LoginResult;

    public sealed record InvalidPin : LoginResult;

    /// <summary>Admin email+password login: unknown email, wrong password, or an
    /// account with no email/password set at all. Deliberately one case, same as
    /// InvalidDevice/InvalidPin above — no need to tell a caller which part was wrong.</summary>
    public sealed record InvalidAdminCredentials : LoginResult;
}
