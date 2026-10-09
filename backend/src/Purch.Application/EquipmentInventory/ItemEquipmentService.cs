using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.EquipmentInventory;

/// <summary>The equipment an Item needs. While any of it is out of service the item shows as out of stock and
/// cannot be sold — see EquipmentAvailability.</summary>
public sealed class ItemEquipmentService(
    IItemEquipmentRepository itemEquipmentRepository,
    IEquipmentRepository equipmentRepository,
    IItemRepository itemRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IItemEquipmentService
{
    public async Task<IReadOnlyList<ItemEquipmentDto>> GetAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await EnsureOwnedItemAsync(itemId, cancellationToken);

        var links = await itemEquipmentRepository.ListByItemAsync(itemId, cancellationToken);
        return await ToDtosAsync([.. links.Select(link => link.EquipmentId)], cancellationToken);
    }

    public async Task<IReadOnlyList<ItemEquipmentDto>> ReplaceAsync(Guid itemId, ReplaceItemEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureOwnedItemAsync(itemId, cancellationToken);

        var wanted = request.EquipmentIds.Distinct().ToList();
        var found = await equipmentRepository.ListByIdsAsync(wanted, cancellationToken);
        foreach (var id in wanted)
        {
            var equipment = found.FirstOrDefault(row => row.Id == id);
            if (equipment is null || equipment.TenantId != CurrentTenantId || equipment.IsDeleted)
            {
                throw new NotFoundException("Equipment", id);
            }
        }

        var existing = await itemEquipmentRepository.ListByItemAsync(itemId, cancellationToken);
        itemEquipmentRepository.RemoveRange(existing.Where(link => !wanted.Contains(link.EquipmentId)));
        itemEquipmentRepository.AddRange(wanted
            .Where(id => existing.All(link => link.EquipmentId != id))
            .Select(id => new ItemEquipment { TenantId = CurrentTenantId, ItemId = itemId, EquipmentId = id }));

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtosAsync(wanted, cancellationToken);
    }

    private async Task EnsureOwnedItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        if (item.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("Item", itemId);
        }
    }

    private async Task<IReadOnlyList<ItemEquipmentDto>> ToDtosAsync(IReadOnlyCollection<Guid> equipmentIds, CancellationToken cancellationToken)
    {
        var equipment = await equipmentRepository.ListByIdsAsync(equipmentIds, cancellationToken);
        return [.. equipment
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Select(row => new ItemEquipmentDto(row.Id, row.Name, row.Status))];
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Item equipment management requires an authenticated tenant context.");
}
