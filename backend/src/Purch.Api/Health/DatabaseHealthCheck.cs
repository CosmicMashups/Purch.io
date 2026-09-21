using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Purch.Infrastructure.Persistence;

namespace Purch.Api.Health;

/// <summary>
/// Readiness: can this instance actually reach its database right now? Deliberately a separate probe from
/// <c>/health</c> (liveness, always OK while the process runs): a database blip should take an instance out of
/// rotation, not get the process killed and restarted. Runs a trivial query under a short timeout so a hung
/// connection reports unhealthy instead of hanging the probe.
/// </summary>
public sealed class DatabaseHealthCheck(PurchDbContext dbContext) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            // Not CanConnectAsync: that swallows the reason. This surfaces it in the (server-side only) check result.
            _ = await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", timeout.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("The database is unreachable.", exception);
        }
    }
}
