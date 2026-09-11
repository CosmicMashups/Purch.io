using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Purch.Api.Middleware;
using Purch.Application.Auth;

namespace Purch.UnitTests;

public sealed class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task Authenticated_request_missing_a_tenant_claim_is_rejected_with_401()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "TestAuth")),
        };
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_request_with_a_valid_tenant_claim_passes_through()
    {
        var claims = new[] { new Claim(JwtClaimTypes.TenantId, Guid.NewGuid().ToString()) };
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth")),
        };

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_passes_through_untouched()
    {
        var context = new DefaultHttpContext();

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
