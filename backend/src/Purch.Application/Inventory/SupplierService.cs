using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public sealed class SupplierService(
    ISupplierRepository supplierRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : ISupplierService
{
    public async Task<IReadOnlyList<SupplierDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var suppliers = await supplierRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. suppliers.OrderBy(supplier => supplier.Name).Select(ToDto)];
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Supplier name is required.");
        }

        var supplier = new Supplier
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            ContactInfo = request.ContactInfo?.Trim(),
            IsActive = true,
        };

        supplierRepository.Add(supplier);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(supplier);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Supplier management requires an authenticated tenant context.");

    private static SupplierDto ToDto(Supplier supplier)
    {
        return new(supplier.Id, supplier.Name, supplier.ContactInfo, supplier.IsActive);
    }
}
