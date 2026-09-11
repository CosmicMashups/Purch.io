using Microsoft.EntityFrameworkCore;
using Purch.Common.TestUtilities;
using Purch.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Purch.IntegrationTests.Fixtures;

/// <summary>
/// One ephemeral Postgres container per test collection, migrated once. Using a real
/// Postgres (not EF Core's InMemory provider) matters here because tenant-isolation and
/// RBAC behavior depend on real relational query execution, not an in-memory approximation.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("purch_test")
        .WithUsername("purch_test")
        .WithPassword("purch_test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<PurchDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new PurchDbContext(options, new TestCurrentTenantProvider());
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
