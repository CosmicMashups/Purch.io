namespace Purch.Application.Reporting;

public interface IInventoryValuationService
{
    Task<InventoryValuationDto> GetAsync(InventoryValuationQuery query, CancellationToken cancellationToken = default);
}
