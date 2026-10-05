using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public interface IIncomingReceivingRepository
{
    Task<IReadOnlyList<IncomingReceivingReport>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<IncomingReceivingReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncomingReceivingReportLine>> ListLinesAsync(Guid reportId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncomingReceivingReport>> ListByPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);

    void Add(IncomingReceivingReport report);

    void AddLine(IncomingReceivingReportLine line);
}
