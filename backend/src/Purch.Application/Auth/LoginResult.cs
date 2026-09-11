namespace Purch.Application.Auth;

public abstract record LoginResult
{
    public sealed record Success(string AccessToken) : LoginResult;

    public sealed record InvalidDevice : LoginResult;

    public sealed record InvalidPin : LoginResult;
}
