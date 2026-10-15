using Microsoft.EntityFrameworkCore;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfPurchaseRequestRepository(PurchDbContext dbContext) : IPurchaseRequestRepository
{
    public async Task<IReadOnlyList<PurchaseRequest>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await dbContext.PurchaseRequests.AsNoTracking().Where(r => r.TenantId == tenantId).ToListAsync(cancellationToken);

    public Task<PurchaseRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PurchaseRequestLine>> ListLinesAsync(Guid purchaseRequestId, CancellationToken cancellationToken = default) =>
        await dbContext.PurchaseRequestLines.Where(l => l.PurchaseRequestId == purchaseRequestId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PurchaseRequestLine>> ListActiveLinesAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await dbContext.PurchaseRequestLines.AsNoTracking()
            .Join(
                dbContext.PurchaseRequests.Where(r => r.TenantId == tenantId && (r.Status == PurchaseRequestStatus.Open || r.Status == PurchaseRequestStatus.Approved)),
                line => line.PurchaseRequestId,
                request => request.Id,
                (line, _) => line)
            .ToListAsync(cancellationToken);

    public void Add(PurchaseRequest request) => _ = dbContext.PurchaseRequests.Add(request);

    public void AddLine(PurchaseRequestLine line) => _ = dbContext.PurchaseRequestLines.Add(line);

    public async Task<IReadOnlyList<ItemSupplier>> ListSupplierLinksAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await dbContext.ItemSuppliers.AsNoTracking().Where(l => l.TenantId == tenantId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ItemSupplier>> ListSupplierLinksForAsync(Guid? itemId, Guid? inventoryItemId, CancellationToken cancellationToken = default) =>
        await dbContext.ItemSuppliers
            .Where(l => (itemId != null && l.ItemId == itemId) || (inventoryItemId != null && l.InventoryItemId == inventoryItemId))
            .ToListAsync(cancellationToken);

    public void AddSupplierLink(ItemSupplier link) => _ = dbContext.ItemSuppliers.Add(link);

    public void RemoveSupplierLink(ItemSupplier link) => _ = dbContext.ItemSuppliers.Remove(link);

    public async Task<DocumentSequence> GetOrCreateSequenceAsync(Guid tenantId, Guid branchId, string kind, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.DocumentSequences.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.BranchId == branchId && s.Kind == kind, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new DocumentSequence { TenantId = tenantId, BranchId = branchId, Kind = kind };
        _ = dbContext.DocumentSequences.Add(created);
        return created;
    }
}
