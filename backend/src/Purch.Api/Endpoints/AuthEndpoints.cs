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
                LoginResult.Success success => Results.Ok(new { accessToken = success.AccessToken }),
                LoginResult.InvalidDevice => Results.Unauthorized(),
                LoginResult.InvalidPin => Results.Unauthorized(),
                _ => Results.Problem("Unhandled login result."),
            };
        }).AllowAnonymous();

        return app;
    }
}
