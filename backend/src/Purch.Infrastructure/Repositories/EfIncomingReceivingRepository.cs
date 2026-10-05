using Microsoft.EntityFrameworkCore;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfIncomingReceivingRepository(PurchDbContext dbContext) : IIncomingReceivingRepository
{
    public async Task<IReadOnlyList<IncomingReceivingReport>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.IncomingReceivingReports
            .AsNoTracking()
            .Where(report => report.TenantId == tenantId)
            .OrderByDescending(report => report.DeliveryDate)
            .ThenByDescending(report => report.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<IncomingReceivingReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.IncomingReceivingReports.FirstOrDefaultAsync(report => report.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<IncomingReceivingReportLine>> ListLinesAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        return await dbContext.IncomingReceivingReportLines
            .Where(line => line.ReportId == reportId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncomingReceivingReport>> ListByPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        return await dbContext.IncomingReceivingReports
            .AsNoTracking()
            .Where(report => report.PurchaseOrderId == purchaseOrderId)
            .OrderBy(report => report.DeliveryDate)
            .ToListAsync(cancellationToken);
    }

    public void Add(IncomingReceivingReport report)
    {
        _ = dbContext.IncomingReceivingReports.Add(report);
    }

    public void AddLine(IncomingReceivingReportLine line)
    {
        _ = dbContext.IncomingReceivingReportLines.Add(line);
    }
}
