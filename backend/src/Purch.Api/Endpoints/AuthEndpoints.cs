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

                // Deliberately the same status + message for both failure cases: telling a
                // caller "that device doesn't exist" vs "that PIN is wrong" would let someone
                // probe for valid device pairing codes one guess at a time.
                LoginResult.InvalidDevice or LoginResult.InvalidPin => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid credentials.",
                    detail: "The device pairing code or PIN was not recognized."),

                _ => throw new InvalidOperationException($"Unhandled {nameof(LoginResult)} case: {result.GetType().Name}"),
            };
        }).AllowAnonymous();

        return app;
    }
}
