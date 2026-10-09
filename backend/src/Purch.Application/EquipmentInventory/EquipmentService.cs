using System.Text.Json;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.EquipmentInventory;

/// <summary>CRUD for Equipment: durable things like an ice cream machine, a deep fryer or a set of utensils. A status
/// change is audited because it decides whether items can be sold; the catalog picks it up on its next poll.</summary>
public sealed class EquipmentService(
    IEquipmentRepository equipmentRepository,
    IItemEquipmentRepository itemEquipmentRepository,
    IAuditLogRepository auditLogRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IEquipmentService
{
    public async Task<IReadOnlyList<EquipmentDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var equipment = await equipmentRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var usedBy = await itemEquipmentRepository.CountItemsByEquipmentAsync(CurrentTenantId, cancellationToken);

        return [.. equipment
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Select(row => ToDto(row) with { UsedByItemCount = usedBy.GetValueOrDefault(row.Id) })];
    }

    public async Task<EquipmentDto> CreateAsync(CreateEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.Name, request.Kind, request.Quantity, request.Status);

        var equipment = new Equipment
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            Kind = request.Kind,
            Status = request.Status,
            Quantity = request.Quantity,
            Location = NullIfBlank(request.Location),
            Notes = NullIfBlank(request.Notes),
            IsActive = true,
        };

        equipmentRepository.Add(equipment);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(equipment);
    }

    public async Task<EquipmentDto> UpdateAsync(Guid equipmentId, UpdateEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.Name, request.Kind, request.Quantity, EquipmentStatus.Operational);

        var equipment = await GetOwnedAsync(equipmentId, cancellationToken);
        equipment.Name = request.Name.Trim();
        equipment.Kind = request.Kind;
        equipment.Quantity = request.Quantity;
        equipment.Location = NullIfBlank(request.Location);
        equipment.Notes = NullIfBlank(request.Notes);
        equipment.IsActive = request.IsActive;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(equipment);
    }

    public async Task<EquipmentDto> SetStatusAsync(Guid equipmentId, SetEquipmentStatusRequest request, CancellationToken cancellationToken = default)
    {
        Validate("status", EquipmentKind.Equipment, null, request.Status);

        var equipment = await GetOwnedAsync(equipmentId, cancellationToken);
        if (equipment.Status != request.Status)
        {
            auditLogRepository.Add(new AuditLog
            {
                TenantId = CurrentTenantId,
                ActorUserId = CurrentUserId,
                ActionType = AuditActionType.EquipmentStatusChanged,
                TargetEntityType = nameof(Equipment),
                TargetEntityId = equipment.Id,
                BeforeStateJson = JsonSerializer.Serialize(new { name = equipment.Name, status = equipment.Status.ToString() }),
                AfterStateJson = JsonSerializer.Serialize(new { name = equipment.Name, status = request.Status.ToString() }),
            });

            equipment.Status = request.Status;
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToDto(equipment);
    }

    public async Task ReorderAsync(ReorderEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        for (var position = 0; position < request.EquipmentIds.Count; position++)
        {
            var equipment = await GetOwnedAsync(request.EquipmentIds[position], cancellationToken);
            equipment.SortOrder = position;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(string name, EquipmentKind kind, int? quantity, EquipmentStatus status)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "Equipment name is required.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ValidationException(nameof(kind), "Unknown equipment type.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new ValidationException(nameof(status), "Unknown equipment status.");
        }

        if (quantity is < 0)
        {
            throw new ValidationException(nameof(quantity), "Quantity cannot be negative.");
        }
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<Equipment> GetOwnedAsync(Guid equipmentId, CancellationToken cancellationToken)
    {
        var equipment = await equipmentRepository.GetByIdAsync(equipmentId, cancellationToken)
            ?? throw new NotFoundException("Equipment", equipmentId);

        return equipment.TenantId != CurrentTenantId || equipment.IsDeleted
            ? throw new NotFoundException("Equipment", equipmentId)
            : equipment;
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Equipment management requires an authenticated tenant context.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("Equipment management requires an authenticated staff user.");

    private static EquipmentDto ToDto(Equipment equipment)
    {
        return new(
            equipment.Id,
            equipment.Name,
            equipment.Kind,
            equipment.Status,
            equipment.Quantity,
            equipment.Location,
            equipment.Notes,
            equipment.IsActive,
            equipment.SortOrder);
    }
}
