using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Domain.Entities;
using Purch.Infrastructure.Storage;

namespace Purch.IntegrationTests.Fixtures;

/// <summary>Boots the real Api pipeline (JWT auth, TenantResolutionMiddleware, DI) against the test Postgres container.</summary>
public sealed class PurchApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string TestJwtSigningKey = "integration-test-signing-key-do-not-use-in-prod";
    public const string TestJwtIssuer = "purch.io.tests";

    /// <summary>Extra configuration for one test (set before the first request creates the host), e.g.
    /// <c>new PurchApiFactory(cs) { ExtraSettings = { ["PUBLIC_BASE_URL"] = "https://cdn.example" } }</c>.</summary>
    public Dictionary<string, string?> ExtraSettings { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _ = builder.ConfigureAppConfiguration((webHostBuilderContext, configBuilder) =>
        {
            _ = configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PURCH_DEPLOYMENT_MODE"] = "Cloud",
                ["SUPABASE_DB_CONNECTION_STRING"] = connectionString,
                ["SUPABASE_STORAGE_URL"] = "https://test.local/storage",
                ["SUPABASE_STORAGE_KEY"] = "integration-test-storage-key",
                ["JWT_SIGNING_KEY"] = TestJwtSigningKey,
                ["JWT_ISSUER"] = TestJwtIssuer,
                // The suite's many payment calls predate the field; tests of the rule itself switch it on
                // through ExtraSettings (see the ExpectedTotal tests in PosEndpointsTests).
                ["POS_REQUIRE_EXPECTED_TOTAL"] = "false",
            });
            _ = configBuilder.AddInMemoryCollection(ExtraSettings);
        });

        _ = builder.ConfigureServices(services =>
        {
            // Cloud mode would check passwords with Supabase Auth, which has no project behind it in tests.
            _ = services.RemoveAll<IIdentityProvider>();
            _ = services.AddScoped<IIdentityProvider, Purch.Infrastructure.Auth.LocalIdentityProvider>();

            // Real IFileStorage in Cloud mode hits Supabase Storage over HTTP, which
            // has nothing to talk to in tests (SUPABASE_STORAGE_URL above is a fake
            // host). Swap in LocalFileStorage against a temp dir so upload tests
            // still exercise the endpoint end-to-end without real network I/O.
            _ = services.RemoveAll<IFileStorage>();
            _ = services.AddSingleton<IFileStorage>(
                new LocalFileStorage(Path.Combine(Path.GetTempPath(), "purch-test-uploads", Guid.NewGuid().ToString("N"))));

            // Every test class shares one Postgres container (see PostgresContainerFixture), so a real
            // background sweep here would race other tests' rows — including RetentionSweeperTests'
            // own deliberately-old rows — with no ordering guarantee. Retention logic is covered directly
            // against RetentionSweeper instead; nothing here needs it actually running.
            _ = services.RemoveAll<IHostedService>();
        });
    }
}
