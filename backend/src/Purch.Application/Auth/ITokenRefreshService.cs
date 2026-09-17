namespace Purch.Application.Auth;

public interface ITokenRefreshService
{
    /// <summary>Redeems a refresh token for a fresh access token plus a new, rotated
    /// refresh token (the old one can never be redeemed again either way).</summary>
    Task<TokenRefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public abstract record TokenRefreshResult
{
    public sealed record Success(string AccessToken, string RefreshToken) : TokenRefreshResult;

    /// <summary>Covers a missing/malformed token, an expired one, an already-used
    /// (rotated) one, and one whose owning user/device no longer exists — deliberately
    /// one case, same rationale as LoginResult's InvalidDevice/InvalidPin.</summary>
    public sealed record InvalidToken : TokenRefreshResult;
}
