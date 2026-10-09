namespace Purch.Application.EquipmentInventory;

public interface IEquipmentService
{
    Task<IReadOnlyList<EquipmentDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<EquipmentDto> CreateAsync(CreateEquipmentRequest request, CancellationToken cancellationToken = default);

    Task<EquipmentDto> UpdateAsync(Guid equipmentId, UpdateEquipmentRequest request, CancellationToken cancellationToken = default);

    Task<EquipmentDto> SetStatusAsync(Guid equipmentId, SetEquipmentStatusRequest request, CancellationToken cancellationToken = default);

    Task ReorderAsync(ReorderEquipmentRequest request, CancellationToken cancellationToken = default);
}

public interface IItemEquipmentService
{
    Task<IReadOnlyList<ItemEquipmentDto>> GetAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemEquipmentDto>> ReplaceAsync(Guid itemId, ReplaceItemEquipmentRequest request, CancellationToken cancellationToken = default);
}
