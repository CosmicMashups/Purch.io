using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
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
    public async Task Request_with_a_device_claim_but_no_session_version_claim_is_rejected_even_for_a_never_reset_device()
    {
        // A missing claim used to parse as 0 and so matched any device whose SessionVersion was still 0.
        var device = new Device { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), SessionVersion = 0 };
        var claims = new[]
        {
            new Claim(JwtClaimTypes.TenantId, device.TenantId.ToString()),
            new Claim(JwtClaimTypes.DeviceId, device.Id.ToString()),
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

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
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

    [Fact]
    public async Task Device_session_state_is_read_once_per_ttl_window_when_a_cache_is_supplied()
    {
        var device = new Device { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), SessionVersion = 1 };
        var repository = new FakeDeviceRepository(device);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var calls = 0;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        for (var i = 0; i < 3; i++)
        {
            var context = DeviceContext(device, sessionVersion: 1);
            await middleware.InvokeAsync(context, repository, cache);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        Assert.Equal(3, calls);
        Assert.Equal(1, repository.SessionStateReads);
    }

    [Fact]
    public async Task A_device_that_is_not_found_is_not_cached_so_it_is_rechecked_every_request()
    {
        var unknown = new Device { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), SessionVersion = 1 };
        var repository = new FakeDeviceRepository();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        for (var i = 0; i < 2; i++)
        {
            var context = DeviceContext(unknown, sessionVersion: 1);
            context.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(context, repository, cache);
            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        }

        Assert.Equal(2, repository.SessionStateReads);
    }

    private static DefaultHttpContext DeviceContext(Device device, int sessionVersion)
    {
        var claims = new[]
        {
            new Claim(JwtClaimTypes.TenantId, device.TenantId.ToString()),
            new Claim(JwtClaimTypes.DeviceId, device.Id.ToString()),
            new Claim(JwtClaimTypes.DeviceSessionVersion, sessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth")),
        };
    }

    private sealed class FakeDeviceRepository(Device? device = null) : IDeviceRepository
    {
        public int SessionStateReads { get; private set; }

        public Task<Device?> FindByPairingCodeHashAsync(string pairingCodeHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Device?> FindByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Device?>(null);
        }

        public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(device?.Id == id ? device : null);
        }

        public Task<DeviceSessionState?> GetSessionStateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            SessionStateReads++;
            return Task.FromResult(device?.Id == id
                ? new DeviceSessionState(device.Status, device.IsDeleted, device.SessionVersion)
                : null);
        }

        public Task<Device?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return GetByIdAsync(id, cancellationToken);
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
