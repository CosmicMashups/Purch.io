using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    /// <summary>The real notifier only writes to stdout (no email provider exists yet —
    /// see ConsolePasswordResetTokenNotifier), which tests can't observe. This capturing
    /// double stands in for it so password-reset tests can retrieve the raw token that
    /// would otherwise only ever reach the user's inbox.</summary>
    public CapturingPasswordResetTokenNotifier PasswordResetTokenNotifier { get; } = new();

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
            });
        });

        _ = builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPasswordResetTokenNotifier>();
            services.AddSingleton<IPasswordResetTokenNotifier>(PasswordResetTokenNotifier);

            // Real IFileStorage in Cloud mode hits Supabase Storage over HTTP, which
            // has nothing to talk to in tests (SUPABASE_STORAGE_URL above is a fake
            // host). Swap in LocalFileStorage against a temp dir so upload tests
            // still exercise the endpoint end-to-end without real network I/O.
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(
                new LocalFileStorage(Path.Combine(Path.GetTempPath(), "purch-test-uploads", Guid.NewGuid().ToString("N"))));
        });
    }
}

public sealed class CapturingPasswordResetTokenNotifier : IPasswordResetTokenNotifier
{
    private readonly ConcurrentDictionary<Guid, string> _tokensByUserId = new();

    public Task NotifyAsync(User user, string rawToken, CancellationToken cancellationToken = default)
    {
        _tokensByUserId[user.Id] = rawToken;
        return Task.CompletedTask;
    }

    public string LastTokenFor(Guid userId) => _tokensByUserId[userId];
}
