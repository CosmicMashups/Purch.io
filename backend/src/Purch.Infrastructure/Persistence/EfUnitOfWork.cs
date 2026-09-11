using Purch.Application.Common;

namespace Purch.Infrastructure.Persistence;

public sealed class EfUnitOfWork(PurchDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
