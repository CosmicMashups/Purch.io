using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Common.TestUtilities;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Auth;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuthEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly BCryptPinHasher PinHasher = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Login_with_correct_pairing_code_and_pin_issues_a_token_with_the_right_tenant_claim()
    {
        var tenantId = Guid.NewGuid();
        const string pairingCode = "DEVICE-CORRECT-PIN";
        const string pin = "1234";
        await SeedTenantDeviceAndUserAsync(tenantId, pairingCode, pin, Role.Admin);

        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(pairingCode, pin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponseBody>(JsonOptions);
        Assert.NotNull(body);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body!.AccessToken);
        var tenantClaim = jwt.Claims.Single(c => c.Type == JwtClaimTypes.TenantId).Value;
        var roleClaim = jwt.Claims.Single(c => c.Type == JwtClaimTypes.Role).Value;

        Assert.Equal(tenantId.ToString(), tenantClaim);
        Assert.Equal(nameof(Role.Admin), roleClaim);
    }

    [Fact]
    public async Task Login_with_wrong_pin_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        const string pairingCode = "DEVICE-WRONG-PIN";
        await SeedTenantDeviceAndUserAsync(tenantId, pairingCode, "1234", Role.Cashier);

        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(pairingCode, "9999"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_device_pairing_code_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("NO-SUCH-DEVICE", "1234"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task SeedTenantDeviceAndUserAsync(Guid tenantId, string pairingCode, string pin, Role role)
    {
        var options = new DbContextOptionsBuilder<PurchDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        await using var dbContext = new PurchDbContext(options, new TestCurrentTenantProvider { TenantId = tenantId });

        var branch = new Branch { TenantId = tenantId, Name = "Main Branch" };
        _ = dbContext.Branches.Add(branch);

        _ = dbContext.Devices.Add(new Device
        {
            TenantId = tenantId,
            BranchId = branch.Id,
            PairingCode = pairingCode,
        });

        _ = dbContext.Users.Add(new User
        {
            TenantId = tenantId,
            Name = "Test Staff",
            Role = role,
            ScopeType = ScopeType.Tenant,
            PinHash = PinHasher.Hash(pin),
            IsActive = true,
        });

        _ = await dbContext.SaveChangesAsync();
    }

    private sealed record LoginResponseBody(string AccessToken);
}
