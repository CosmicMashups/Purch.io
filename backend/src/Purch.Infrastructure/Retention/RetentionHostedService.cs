using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Purch.Infrastructure.Retention;

/// <summary>
/// Runs RetentionSweeper on a fixed interval for the lifetime of the process. A short random startup
/// delay avoids every instance in a multi-instance deployment sweeping at the same instant; a fresh
/// DI scope per run gives RetentionSweeper its own short-lived DbContext rather than one held for the
/// process lifetime. A single sweep failing is logged and never crashes the app or stops future sweeps.
/// </summary>
public sealed partial class RetentionHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    ILogger<RetentionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Random.Shared.Next(1_000, 30_000), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.IntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sweeper = scope.ServiceProvider.GetRequiredService<RetentionSweeper>();
            var result = await sweeper.RunAsync(cancellationToken);

            if (result.Total > 0)
            {
                LogSwept(
                    logger,
                    result.RefreshTokensPurged,
                    result.PasswordResetTokensPurged,
                    result.SyncedRecordsPurged,
                    result.AuditLogsPurged,
                    result.InventoryMovementsPurged);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSweepFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Retention sweep purged {RefreshTokens} refresh token(s), " +
        "{PasswordResetTokens} password reset token(s), {SyncedRecords} synced record(s), {AuditLogs} audit log(s), " +
        "{InventoryMovements} inventory movement(s).")]
    private static partial void LogSwept(
        ILogger logger, int refreshTokens, int passwordResetTokens, int syncedRecords, int auditLogs, int inventoryMovements);

    [LoggerMessage(Level = LogLevel.Error, Message = "Retention sweep failed; will retry on the next interval.")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
