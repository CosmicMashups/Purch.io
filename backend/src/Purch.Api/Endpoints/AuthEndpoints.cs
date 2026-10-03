using Purch.Api.RateLimiting;
using Purch.Application.Auth;

namespace Purch.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Email and password, for a personal device (no device pairing, so no selling). A person who belongs to several
        // businesses gets the list back and signs in again with the tenant they chose.
        _ = app.MapPost("/auth/sign-in", async (SignInRequest request, IAccountService accountService, CancellationToken cancellationToken) =>
        {
            var result = await accountService.SignInAsync(request, cancellationToken);
            return result switch
            {
                SignInResult.Success success => Results.Ok(new { accessToken = success.AccessToken, refreshToken = success.RefreshToken }),

                SignInResult.ChooseBusiness choose => Results.Ok(new { chooseBusiness = true, businesses = choose.Businesses }),

                SignInResult.Invalid => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid credentials.",
                    detail: "The email or password was not recognized."),

                _ => throw new InvalidOperationException($"Unhandled {nameof(SignInResult)} case: {result.GetType().Name}"),
            };
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.AuthSensitive);

        _ = app.MapPost("/auth/refresh", async (RefreshTokenRequest request, ITokenRefreshService tokenRefreshService, CancellationToken cancellationToken) =>
        {
            var result = await tokenRefreshService.RefreshAsync(request.RefreshToken, cancellationToken);
            return result switch
            {
                TokenRefreshResult.Success success => Results.Ok(new { accessToken = success.AccessToken, refreshToken = success.RefreshToken }),

                TokenRefreshResult.InvalidToken => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid refresh token.",
                    detail: "The refresh token was not recognized, expired, or already used."),

                _ => throw new InvalidOperationException($"Unhandled {nameof(TokenRefreshResult)} case: {result.GetType().Name}"),
            };
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.Refresh);

        // Best-effort: revokes the refresh token so it can't be redeemed later, but
        // never fails the client's own logout flow (see IRefreshTokenService.RevokeAsync).
        _ = app.MapPost("/auth/logout", async (RefreshTokenRequest request, IRefreshTokenService refreshTokenService, CancellationToken cancellationToken) =>
        {
            await refreshTokenService.RevokeAsync(request.RefreshToken, cancellationToken);
            return Results.NoContent();
        }).AllowAnonymous();

        return app;
    }
}
