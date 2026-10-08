using Microsoft.EntityFrameworkCore;
using Purch.Common.TestUtilities;
using Purch.Infrastructure.Persistence;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests.Security;

/// <summary>Every table in the public schema must have Row Level Security switched on. This backend connects as a role that
/// bypasses RLS, so a forgotten table is invisible to the app but readable through Supabase's REST API with the public anon
/// key. A new migration that creates a table without ENABLE ROW LEVEL SECURITY fails here.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class RowLevelSecurityCoverageTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Every_public_table_has_row_level_security_enabled()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = factory.CreateClient(); // starts the app, which applies migrations

        var options = new DbContextOptionsBuilder<PurchDbContext>().UseNpgsql(postgres.ConnectionString).Options;
        await using var dbContext = new PurchDbContext(options, new TestCurrentTenantProvider());

        var unprotected = await dbContext.Database
            .SqlQueryRaw<string>(
                "SELECT c.relname AS \"Value\" FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace " +
                "WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p') AND NOT c.relrowsecurity " +
                "AND c.relname <> '__EFMigrationsHistory' ORDER BY c.relname")
            .ToListAsync();

        Assert.True(unprotected.Count == 0, "Tables without Row Level Security: " + string.Join(", ", unprotected));
    }
}
