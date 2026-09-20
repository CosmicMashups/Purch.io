using Purch.Api.RateLimiting;
using Purch.Application.Auth;

namespace Purch.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        _ = app.MapPost("/auth/login", async (LoginRequest request, ILoginService loginService, CancellationToken cancellationToken) =>
        {
            var result = await loginService.LoginAsync(request, cancellationToken);
            return result switch
            {
                LoginResult.Success success => Results.Ok(new { accessToken = success.AccessToken, refreshToken = success.RefreshToken }),

                // Deliberately the same status + message for both failure cases: telling a
                // caller "that device doesn't exist" vs "that PIN is wrong" would let someone
                // probe for valid device pairing codes one guess at a time.
                LoginResult.InvalidDevice or LoginResult.InvalidPin => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid credentials.",
                    detail: "The device pairing code or PIN was not recognized."),

                _ => throw new InvalidOperationException($"Unhandled {nameof(LoginResult)} case: {result.GetType().Name}"),
            };
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.AuthSensitive);

        _ = app.MapPost("/auth/admin-login", async (AdminLoginRequest request, ILoginService loginService, CancellationToken cancellationToken) =>
        {
            var result = await loginService.AdminLoginAsync(request, cancellationToken);
            return result switch
            {
                LoginResult.Success success => Results.Ok(new { accessToken = success.AccessToken, refreshToken = success.RefreshToken }),

                LoginResult.InvalidAdminCredentials => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid credentials.",
                    detail: "The email or password was not recognized."),

                _ => throw new InvalidOperationException($"Unhandled {nameof(LoginResult)} case: {result.GetType().Name}"),
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

        // Always 204 regardless of whether the email exists — see
        // IPasswordResetService.RequestAsync for why. Rate-limited (see Program.cs) since
        // this is an anonymous, repeatable action.
        _ = app.MapPost("/auth/password-reset/request", async (
            PasswordResetRequest request,
            IPasswordResetService passwordResetService,
            CancellationToken cancellationToken) =>
        {
            await passwordResetService.RequestAsync(request.Email, cancellationToken);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.AuthSensitive);

        _ = app.MapPost("/auth/password-reset/confirm", async (
            PasswordResetConfirmRequest request,
            IPasswordResetService passwordResetService,
            CancellationToken cancellationToken) =>
        {
            var result = await passwordResetService.ConfirmAsync(request.Token, request.NewPassword, cancellationToken);
            return result switch
            {
                PasswordResetResult.Success => Results.NoContent(),

                PasswordResetResult.InvalidToken => Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid reset token.",
                    detail: "The reset token was not recognized, expired, or already used."),

                _ => throw new InvalidOperationException($"Unhandled {nameof(PasswordResetResult)} case: {result.GetType().Name}"),
            };
        }).AllowAnonymous().RequireRateLimiting(RateLimiterPolicies.AuthSensitive);

        return app;
    }
}
