using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Purch.IntegrationTests.Fixtures;

/// <summary>Boots the real Api pipeline (JWT auth, TenantResolutionMiddleware, DI) against the test Postgres container.</summary>
public sealed class PurchApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string TestJwtSigningKey = "integration-test-signing-key-do-not-use-in-prod";
    public const string TestJwtIssuer = "purch.io.tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _ = builder.ConfigureAppConfiguration((webHostBuilderContext, configBuilder) =>
        {
            _ = configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PURCH_DEPLOYMENT_MODE"] = "Cloud",
                ["SUPABASE_DB_CONNECTION_STRING"] = connectionString,
                ["SUPABASE_STORAGE_URL"] = "https://test.local/storage",
                ["JWT_SIGNING_KEY"] = TestJwtSigningKey,
                ["JWT_ISSUER"] = TestJwtIssuer,
            });
        });
    }
}
