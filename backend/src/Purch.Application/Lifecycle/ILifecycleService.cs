namespace Purch.Application.Lifecycle;

/// <summary>Every kind of record that can be switched to Inactive or soft-deleted from the back office.</summary>
public enum LifecycleKind
{
    Item,
    Ingredient,
    Category,
    Supplier,
    ModifierGroup,
    Modifier,
    BogoPromo,
    ComboPromo,
    ItemDiscountPromo,
    PromoCode,
    Staff,
    Branch,
    Device,
    Customer,
    Equipment,
}

public enum LifecycleStatus
{
    Active,
    Inactive,
    Deleted,
}

/// <summary>What changing a record would touch, shown in the confirmation dialog. The blocked reasons are set when the
/// change is not allowed at all (an open shift, the last Admin, an unpaid balance).</summary>
public sealed record LifecycleImpactDto(Guid Id, string Name, LifecycleStatus Status, IReadOnlyList<string> Notes, string? DeactivateBlockedReason, string? DeleteBlockedReason);

public sealed record DeletedRecordDto(Guid Id, string Name, DateTimeOffset? DeletedAt);

public sealed record LifecycleResultDto(Guid Id, string Name, LifecycleStatus Status);

/// <summary>Roles are the caller's role names (Admin, Manager, Warehouse, Cashier...).</summary>
public interface ILifecycleService
{
    /// <summary>The soft-deleted records of one kind, for the Deleted view where they can be restored.</summary>
    Task<IReadOnlyList<DeletedRecordDto>> ListDeletedAsync(LifecycleKind kind, IReadOnlySet<string> roles, CancellationToken cancellationToken = default);

    Task<LifecycleImpactDto> GetImpactAsync(LifecycleKind kind, Guid id, IReadOnlySet<string> roles, CancellationToken cancellationToken = default);

    Task<LifecycleResultDto> DeactivateAsync(LifecycleKind kind, Guid id, IReadOnlySet<string> roles, CancellationToken cancellationToken = default);

    Task<LifecycleResultDto> ReactivateAsync(LifecycleKind kind, Guid id, IReadOnlySet<string> roles, CancellationToken cancellationToken = default);

    /// <summary>Soft delete. The row stays; it is also switched to Inactive so nothing can sell or sign in with it.</summary>
    Task<LifecycleResultDto> DeleteAsync(LifecycleKind kind, Guid id, IReadOnlySet<string> roles, CancellationToken cancellationToken = default);

    /// <summary>Brings a deleted record back as Inactive; reactivating it is a separate, deliberate step.</summary>
    Task<LifecycleResultDto> RestoreAsync(LifecycleKind kind, Guid id, IReadOnlySet<string> roles, CancellationToken cancellationToken = default);
}
