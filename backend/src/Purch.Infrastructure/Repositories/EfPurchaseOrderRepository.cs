using Microsoft.EntityFrameworkCore;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfPurchaseOrderRepository(PurchDbContext dbContext) : IPurchaseOrderRepository
{
    public async Task<IReadOnlyList<PurchaseOrder>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(purchaseOrder => purchaseOrder.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.PurchaseOrders.FirstOrDefaultAsync(purchaseOrder => purchaseOrder.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseOrderLine>> ListLinesAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PurchaseOrderLines
            .Where(line => line.PurchaseOrderId == purchaseOrderId)
            .ToListAsync(cancellationToken);
    }

    public Task<PurchaseOrderLine?> GetLineAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        return dbContext.PurchaseOrderLines.FirstOrDefaultAsync(line => line.Id == lineId, cancellationToken);
    }

    public void Add(PurchaseOrder purchaseOrder)
    {
        _ = dbContext.PurchaseOrders.Add(purchaseOrder);
    }

    public void AddLine(PurchaseOrderLine line)
    {
        _ = dbContext.PurchaseOrderLines.Add(line);
    }
}
