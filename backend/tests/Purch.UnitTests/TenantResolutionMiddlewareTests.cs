using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Purch.Api.Middleware;
using Purch.Application.Auth;
using Purch.Domain.Entities;

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

        await middleware.InvokeAsync(context, new FakeDeviceRepository());

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

        await middleware.InvokeAsync(context, new FakeDeviceRepository());

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

        await middleware.InvokeAsync(context, new FakeDeviceRepository());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Request_with_a_device_claim_matching_the_devices_current_session_version_passes_through()
    {
        var device = new Device { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), SessionVersion = 2 };
        var claims = new[]
        {
            new Claim(JwtClaimTypes.TenantId, device.TenantId.ToString()),
            new Claim(JwtClaimTypes.DeviceId, device.Id.ToString()),
            new Claim(JwtClaimTypes.DeviceSessionVersion, "2"),
        };
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

        await middleware.InvokeAsync(context, new FakeDeviceRepository(device));

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Request_with_a_device_claim_stale_relative_to_the_devices_current_session_version_is_rejected_with_401()
    {
        // Simulates a token issued before a pairing reset (device.SessionVersion bumped
        // to 2 after the token carrying "1" was issued) — see DeviceManagementService.
        var device = new Device { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), SessionVersion = 2 };
        var claims = new[]
        {
            new Claim(JwtClaimTypes.TenantId, device.TenantId.ToString()),
            new Claim(JwtClaimTypes.DeviceId, device.Id.ToString()),
            new Claim(JwtClaimTypes.DeviceSessionVersion, "1"),
        };
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth")),
        };
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new FakeDeviceRepository(device));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    private sealed class FakeDeviceRepository(Device? device = null) : IDeviceRepository
    {
        public Task<Device?> FindByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Device?>(null);
        }

        public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(device?.Id == id ? device : null);
        }

        public Task<IReadOnlyList<Device>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Device>>([]);
        }

        public void Add(Device device)
        {
        }
    }
}
