namespace Purch.Application.Inventory;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);
}
