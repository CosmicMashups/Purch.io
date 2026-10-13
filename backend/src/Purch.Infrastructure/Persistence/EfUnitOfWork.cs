using Microsoft.EntityFrameworkCore;
using Purch.Application.Common;

namespace Purch.Infrastructure.Persistence;

public sealed class EfUnitOfWork(PurchDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        // The context uses a retrying execution strategy, which refuses a user transaction unless the whole
        // transaction runs inside strategy.ExecuteAsync (so a retry re-runs it from the start).
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;
        return strategy.ExecuteAsync(async ct =>
        {
            // A retry begins from what the database holds, not from entities a failed attempt left tracked.
            if (attempt++ > 0)
            {
                dbContext.ChangeTracker.Clear();
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            var result = await operation(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }
}
