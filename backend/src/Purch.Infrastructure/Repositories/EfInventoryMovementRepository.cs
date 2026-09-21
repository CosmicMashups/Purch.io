using Microsoft.EntityFrameworkCore;
using Purch.Application.Common;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfInventoryMovementRepository(PurchDbContext dbContext) : IInventoryMovementRepository
{
    public async Task<IReadOnlyList<InventoryMovement>> ListAsync(
        Guid tenantId,
        Guid? itemId,
        Guid? branchId,
        MovementType? type,
        DateTimeOffset? before = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.InventoryMovements
            .AsNoTracking()
            .Where(movement => movement.TenantId == tenantId);

        if (itemId is { } requiredItemId)
        {
            query = query.Where(movement => movement.ItemId == requiredItemId);
        }

        if (branchId is { } requiredBranchId)
        {
            query = query.Where(movement => movement.BranchId == requiredBranchId);
        }

        if (type is { } requiredType)
        {
            query = query.Where(movement => movement.Type == requiredType);
        }

        if (before is { } cursor)
        {
            query = query.Where(movement => movement.CreatedAt < cursor);
        }

        return await query
            .OrderByDescending(movement => movement.CreatedAt)
            .Take(Paging.ClampLimit(limit))
            .ToListAsync(cancellationToken);
    }

    public void Add(InventoryMovement movement)
    {
        _ = dbContext.InventoryMovements.Add(movement);
    }
}
