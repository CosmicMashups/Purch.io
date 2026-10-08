using System.Net;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Security;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class SecurityHeadersTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Api_responses_carry_baseline_hardening_headers()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Hsts_is_sent_only_over_https()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var plain = factory.CreateClient();
        Assert.False((await plain.GetAsync("/health")).Headers.Contains("Strict-Transport-Security"));

        using var secure = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.True((await secure.GetAsync("/health")).Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Error_responses_are_hardened_too()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/transactions/cart");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }
}
