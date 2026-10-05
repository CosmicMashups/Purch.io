namespace Purch.Application.Inventory;

public interface IIncomingReceivingService
{
    Task<IReadOnlyList<IncomingReceivingDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<IncomingReceivingDto> GetAsync(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>Records a delivery. Accepted lines add stock; if a purchase order is given they also count
    /// toward it and move it to Partially Delivered or Delivered.</summary>
    Task<IncomingReceivingDto> CreateAsync(CreateIncomingReceivingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Connects a report recorded without a purchase order to one created afterwards.
    /// Counts its accepted lines toward that order; stock is not added a second time.</summary>
    Task<IncomingReceivingDto> LinkPurchaseOrderAsync(Guid reportId, Guid purchaseOrderId, CancellationToken cancellationToken = default);
}
