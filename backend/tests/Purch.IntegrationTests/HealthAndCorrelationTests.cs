using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Purch.Api.Health;
using Purch.Application.Common;
using Purch.Infrastructure.Persistence;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class HealthAndCorrelationTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Liveness_and_readiness_are_both_ok_when_the_database_is_reachable()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_database_check_reports_unhealthy_instead_of_throwing_when_the_database_is_unreachable()
    {
        // The whole app refuses to start without a database, so this drives the check itself. Nothing listens
        // on port 1, so the connection is refused immediately.
        var options = new DbContextOptionsBuilder<PurchDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2;Command Timeout=2")
            .Options;
        await using var dbContext = new PurchDbContext(options, new NoTenant());

        var result = await new DatabaseHealthCheck(dbContext).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("The database is unreachable.", result.Description);
    }

    private sealed class NoTenant : ICurrentTenantProvider
    {
        public Guid? TenantId => null;
    }

    [Fact]
    public async Task A_supplied_correlation_id_is_echoed_back()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "terminal-7.sale_42");

        var response = await client.SendAsync(request);

        Assert.Equal("terminal-7.sale_42", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task An_unsafe_correlation_id_is_replaced_rather_than_trusted()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "bad id with spaces");

        var response = await client.SendAsync(request);

        var echoed = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.NotEqual("bad id with spaces", echoed);
        Assert.False(string.IsNullOrWhiteSpace(echoed));
    }

    [Fact]
    public async Task Error_responses_carry_the_same_id_in_the_body_and_the_header()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/items");
        request.Headers.Add("X-Correlation-Id", "trace-me-123");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("trace-me-123", body.GetProperty("traceId").GetString());
        Assert.Equal("trace-me-123", response.Headers.GetValues("X-Correlation-Id").Single());
    }
}
