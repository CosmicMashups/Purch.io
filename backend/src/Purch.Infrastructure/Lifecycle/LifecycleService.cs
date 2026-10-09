using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Lifecycle;
using Purch.Application.Onboarding;
using Purch.Domain.Common;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Lifecycle;

/// <summary>
/// One place that switches records to Inactive, soft-deletes and restores them, so every kind follows the same rules:
/// who may do it, what blocks it, what is written to the audit log. Deleting also deactivates, and restoring brings the
/// record back Inactive, so a deleted record can never be sold or signed in with even where a query forgot to look at
/// the delete flag.
/// </summary>
public sealed class LifecycleService(
    PurchDbContext db,
    ICurrentTenantProvider tenantProvider,
    ICurrentActorProvider actorProvider,
    IAuditLogRepository auditLogRepository,
    IRefreshTokenService refreshTokenService,
    IUnitOfWork unitOfWork) : ILifecycleService
{
    private const string Admin = nameof(Role.Admin);
    private const string Manager = nameof(Role.Manager);
    private const string Warehouse = nameof(Role.Warehouse);

    private sealed class Target(ISoftDeletable entity, string name, Func<bool> isActive, Action<bool> setActive)
    {
        public ISoftDeletable Entity { get; } = entity;

        public string Name { get; } = name;

        public bool IsActive => isActive();

        public void SetActive(bool value) => setActive(value);

        public LifecycleStatus Status => Entity.IsDeleted ? LifecycleStatus.Deleted : IsActive ? LifecycleStatus.Active : LifecycleStatus.Inactive;
    }

    public async Task<IReadOnlyList<DeletedRecordDto>> ListDeletedAsync(LifecycleKind kind, IReadOnlySet<string> roles, CancellationToken cancellationToken = default)
    {
        RequireRole(kind, roles, delete: true);
        var rows = kind switch
        {
            LifecycleKind.Item => await db.Items.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.Ingredient => await db.InventoryItems.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.Equipment => await db.EquipmentItems.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.Category => await db.Categories.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.Supplier => await db.Suppliers.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.ModifierGroup => await db.ModifierGroups.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.Modifier => await db.ItemModifiers.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.BogoPromo => await db.BogoPromoRules.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.ComboPromo => await db.ComboPromoRules.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.ItemDiscountPromo => await db.ItemDiscountPromoRules.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.PromoCode => await db.PromoCodes.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Code, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.Staff => await db.Memberships.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Account!.DisplayName, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.Branch => await db.Branches.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name, x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.Device => await db.Devices.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.Name ?? x.DeviceType.ToString(), x.DeletedAt)).ToListAsync(cancellationToken),
            LifecycleKind.Customer => await db.CustomerCreditLedgers.Where(x => x.IsDeleted).Select(x => new DeletedRecordDto(x.Id, x.CustomerFullName, x.DeletedAt)).ToListAsync(cancellationToken),
            _ => throw new ValidationException("kind", "Unknown record type."),
        };
        return [.. rows.OrderByDescending(r => r.DeletedAt)];
    }

    public async Task<LifecycleImpactDto> GetImpactAsync(LifecycleKind kind, Guid id, IReadOnlySet<string> roles, CancellationToken cancellationToken = default)
    {
        RequireRole(kind, roles, delete: false);
        var target = await LoadAsync(kind, id, cancellationToken);
        var notes = await NotesAsync(kind, id, cancellationToken);
        var deactivateBlock = await BlockAsync(kind, id, target, roles, delete: false, cancellationToken);
        var deleteBlock = await BlockAsync(kind, id, target, roles, delete: true, cancellationToken);
        return new LifecycleImpactDto(id, target.Name, target.Status, notes, deactivateBlock, deleteBlock);
    }

    public async Task<LifecycleResultDto> DeactivateAsync(LifecycleKind kind, Guid id, IReadOnlySet<string> roles, CancellationToken cancellationToken = default)
    {
        RequireRole(kind, roles, delete: false);
        var target = await LoadAsync(kind, id, cancellationToken);
        await ThrowIfBlockedAsync(kind, id, target, roles, delete: false, cancellationToken);
        var before = target.Status;
        target.SetActive(false);
        await AfterDeactivateAsync(kind, id, cancellationToken);
        return await FinishAsync(kind, id, target, before, AuditActionType.RecordDeactivated, cancellationToken);
    }

    public async Task<LifecycleResultDto> ReactivateAsync(LifecycleKind kind, Guid id, IReadOnlySet<string> roles, CancellationToken cancellationToken = default)
    {
        RequireRole(kind, roles, delete: false);
        var target = await LoadAsync(kind, id, cancellationToken);
        if (target.Entity.IsDeleted)
        {
            throw new ConflictException("This record is deleted. Restore it first.");
        }

        if (kind == LifecycleKind.Device && await db.Devices.AnyAsync(d => d.Id == id && d.Status != DeviceStatus.Inactive, cancellationToken))
        {
            throw new ConflictException("Only an Inactive device can be reactivated. A revoked device has to be paired again.");
        }

        var before = target.Status;
        target.SetActive(true);
        return await FinishAsync(kind, id, target, before, AuditActionType.RecordReactivated, cancellationToken);
    }

    public async Task<LifecycleResultDto> DeleteAsync(LifecycleKind kind, Guid id, IReadOnlySet<string> roles, CancellationToken cancellationToken = default)
    {
        RequireRole(kind, roles, delete: true);
        var target = await LoadAsync(kind, id, cancellationToken);
        if (target.Entity.IsDeleted)
        {
            return new LifecycleResultDto(id, target.Name, target.Status);
        }

        await ThrowIfBlockedAsync(kind, id, target, roles, delete: true, cancellationToken);
        var before = target.Status;
        target.SetActive(false);
        target.Entity.IsDeleted = true;
        target.Entity.DeletedAt = DateTimeOffset.UtcNow;
        target.Entity.DeletedByUserId = actorProvider.UserId;
        await AfterDeactivateAsync(kind, id, cancellationToken);
        return await FinishAsync(kind, id, target, before, AuditActionType.RecordDeleted, cancellationToken);
    }

    public async Task<LifecycleResultDto> RestoreAsync(LifecycleKind kind, Guid id, IReadOnlySet<string> roles, CancellationToken cancellationToken = default)
    {
        RequireRole(kind, roles, delete: true);
        var target = await LoadAsync(kind, id, cancellationToken);
        if (!target.Entity.IsDeleted)
        {
            return new LifecycleResultDto(id, target.Name, target.Status);
        }

        var before = target.Status;
        target.Entity.IsDeleted = false;
        target.Entity.DeletedAt = null;
        target.Entity.DeletedByUserId = null;
        return await FinishAsync(kind, id, target, before, AuditActionType.RecordRestored, cancellationToken);
    }

    // Warehouse can only handle the four stock-side kinds; everything else is Admin or Manager. A few kinds are
    // tighter than that: only an Admin deletes a device, and only an Admin touches a branch.
    private static void RequireRole(LifecycleKind kind, IReadOnlySet<string> roles, bool delete)
    {
        var isAdmin = roles.Contains(Admin);
        var isManager = roles.Contains(Manager);
        var allowed = kind switch
        {
            LifecycleKind.Item or LifecycleKind.Ingredient or LifecycleKind.Equipment or LifecycleKind.Category or LifecycleKind.Supplier
                => isAdmin || isManager || roles.Contains(Warehouse),
            LifecycleKind.Branch => isAdmin,
            LifecycleKind.Device => delete ? isAdmin : isAdmin || isManager,
            _ => isAdmin || isManager,
        };

        if (!allowed)
        {
            throw new ForbiddenException("You don't have permission to do this.");
        }
    }

    private async Task<Target> LoadAsync(LifecycleKind kind, Guid id, CancellationToken ct)
    {
        switch (kind)
        {
            case LifecycleKind.Item:
                var item = await db.Items.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Item", id);
                return new Target(item, item.Name, () => item.IsActive, v => item.IsActive = v);
            case LifecycleKind.Ingredient:
                var ing = await db.InventoryItems.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Ingredient", id);
                return new Target(ing, ing.Name, () => ing.IsActive, v => ing.IsActive = v);
            case LifecycleKind.Equipment:
                var eq = await db.EquipmentItems.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Equipment", id);
                return new Target(eq, eq.Name, () => eq.IsActive, v => eq.IsActive = v);
            case LifecycleKind.Category:
                var cat = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Category", id);
                return new Target(cat, cat.Name, () => cat.IsActive, v => cat.IsActive = v);
            case LifecycleKind.Supplier:
                var sup = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Supplier", id);
                return new Target(sup, sup.Name, () => sup.IsActive, v => sup.IsActive = v);
            case LifecycleKind.ModifierGroup:
                var grp = await db.ModifierGroups.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Modifier group", id);
                return new Target(grp, grp.Name, () => grp.IsActive, v => grp.IsActive = v);
            case LifecycleKind.Modifier:
                var mod = await db.ItemModifiers.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Modifier", id);
                return new Target(mod, mod.Name, () => mod.IsActive, v => mod.IsActive = v);
            case LifecycleKind.BogoPromo:
                var bogo = await db.BogoPromoRules.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Promotion", id);
                return new Target(bogo, bogo.Name, () => bogo.IsActive, v => bogo.IsActive = v);
            case LifecycleKind.ComboPromo:
                var combo = await db.ComboPromoRules.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Promotion", id);
                return new Target(combo, combo.Name, () => combo.IsActive, v => combo.IsActive = v);
            case LifecycleKind.ItemDiscountPromo:
                var disc = await db.ItemDiscountPromoRules.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Promotion", id);
                return new Target(disc, disc.Name, () => disc.IsActive, v => disc.IsActive = v);
            case LifecycleKind.PromoCode:
                var code = await db.PromoCodes.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Promo code", id);
                return new Target(code, code.Code, () => code.IsActive, v => code.IsActive = v);
            case LifecycleKind.Staff:
                var member = await db.Memberships.Include(x => x.Account).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Person", id);
                return new Target(member, member.Account?.DisplayName ?? "Staff member", () => member.IsActive, v => member.IsActive = v);
            case LifecycleKind.Branch:
                var branch = await db.Branches.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Branch", id);
                return new Target(branch, branch.Name, () => branch.IsActive, v => branch.IsActive = v);
            case LifecycleKind.Device:
                var device = await db.Devices.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Device", id);
                return new Target(
                    device,
                    device.Name ?? device.DeviceType.ToString(),
                    () => device.Status is DeviceStatus.Active or DeviceStatus.Pending,
                    v =>
                    {
                        // Only an Active device can be switched Inactive, and only an Inactive one switched back.
                        // Revoked stays revoked (it needs a new pairing); Pending is left alone.
                        if (!v && device.Status == DeviceStatus.Active)
                        {
                            device.Status = DeviceStatus.Inactive;
                        }
                        else if (v && device.Status == DeviceStatus.Inactive)
                        {
                            device.Status = DeviceStatus.Active;
                        }
                    });
            case LifecycleKind.Customer:
                var customer = await db.CustomerCreditLedgers.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Customer", id);
                return new Target(customer, customer.CustomerFullName, () => customer.IsActive, v => customer.IsActive = v);
            default:
                throw new ValidationException("kind", "Unknown record type.");
        }
    }

    private async Task<IReadOnlyList<string>> NotesAsync(LifecycleKind kind, Guid id, CancellationToken ct)
    {
        var notes = new List<string>();
        switch (kind)
        {
            case LifecycleKind.Item:
                Add(notes, await db.ItemComboComponents.CountAsync(c => c.ComponentItemId == id || c.ParentItemId == id, ct), "combo", "combos");
                Add(notes, await db.BogoPromoRules.CountAsync(p => !p.IsDeleted && p.IsActive && (p.TriggerItemId == id || p.FreeItemId == id), ct), "Buy 1 Take 1 promotion", "Buy 1 Take 1 promotions");
                Add(notes, await db.ComboPromoRules.CountAsync(p => !p.IsDeleted && p.IsActive && (p.ItemAId == id || p.ItemBId == id), ct), "combo deal", "combo deals");
                Add(notes, await db.ItemDiscountPromoRules.CountAsync(p => !p.IsDeleted && p.IsActive && p.ItemId == id, ct), "item discount", "item discounts");
                break;
            case LifecycleKind.Ingredient:
                Add(notes, await db.ItemRecipeLines.Where(r => r.InventoryItemId == id).Select(r => r.ItemId).Distinct().CountAsync(ct), "recipe", "recipes");
                Add(notes, await db.ItemModifierIngredients.CountAsync(m => m.InventoryItemId == id, ct), "modifier", "modifiers");
                break;
            case LifecycleKind.Equipment:
                Add(notes, await db.ItemEquipmentLinks.Where(l => db.Items.Any(i => i.Id == l.ItemId && !i.IsDeleted) && l.EquipmentId == id).CountAsync(ct), "item", "items");
                break;
            case LifecycleKind.Category:
                Add(notes, await db.Items.CountAsync(i => !i.IsDeleted && i.CategoryId == id, ct), "item", "items");
                break;
            case LifecycleKind.Supplier:
                Add(notes, await db.PurchaseOrders.CountAsync(p => p.SupplierId == id && p.Status != PurchaseOrderStatus.Received && p.Status != PurchaseOrderStatus.Cancelled, ct), "open purchase order", "open purchase orders");
                break;
            case LifecycleKind.ModifierGroup:
                Add(notes, await db.ItemModifierGroups.CountAsync(g => g.ModifierGroupId == id, ct), "item", "items");
                break;
            case LifecycleKind.Staff:
                notes.Add("They are signed out right away and can't use their PIN.");
                break;
            case LifecycleKind.Branch:
                Add(notes, await db.Devices.CountAsync(d => !d.IsDeleted && d.BranchId == id, ct), "device", "devices");
                break;
            case LifecycleKind.Device:
                notes.Add("The device is signed out right away.");
                break;
            case LifecycleKind.Customer:
                var balance = await db.CustomerCreditLedgers.Where(c => c.Id == id).Select(c => c.Balance).FirstAsync(ct);
                if (balance > 0)
                {
                    notes.Add($"Owes ₱{balance:N2}. Repayments are still accepted while inactive.");
                }

                break;
            default:
                break;
        }

        return notes;
    }

    private static void Add(List<string> notes, int count, string singular, string plural)
    {
        if (count > 0)
        {
            notes.Add($"Used by {count} {(count == 1 ? singular : plural)}.");
        }
    }

    private async Task ThrowIfBlockedAsync(LifecycleKind kind, Guid id, Target target, IReadOnlySet<string> roles, bool delete, CancellationToken ct)
    {
        var reason = await BlockAsync(kind, id, target, roles, delete, ct);
        if (reason is not null)
        {
            throw new ConflictException(reason);
        }
    }

    private async Task<string?> BlockAsync(LifecycleKind kind, Guid id, Target target, IReadOnlySet<string> roles, bool delete, CancellationToken ct)
    {
        if (target.Entity.IsDeleted && !delete)
        {
            return "This record is deleted. Restore it first.";
        }

        switch (kind)
        {
            case LifecycleKind.Staff:
                var member = await db.Memberships.AsNoTracking().FirstAsync(m => m.Id == id, ct);
                if (actorProvider.UserId is { } me && (me == member.Id || me == member.AccountId))
                {
                    return "You can't do this to your own account.";
                }

                if (member.Role != MembershipRole.Staff && !roles.Contains(Admin))
                {
                    return "Only an Admin can change an Admin or Manager.";
                }

                if (member.Role == MembershipRole.Admin && member.IsActive && await db.Memberships.CountAsync(m => m.Role == MembershipRole.Admin && m.IsActive && !m.IsDeleted, ct) <= 1)
                {
                    return "The business needs at least one active Admin.";
                }

                break;
            case LifecycleKind.Branch:
                if (await db.Shifts.AnyAsync(s => s.BranchId == id && s.Status == ShiftStatus.Open, ct))
                {
                    return "A shift is open at this branch. Close it first.";
                }

                break;
            case LifecycleKind.Device:
                if (await db.Shifts.AnyAsync(s => s.DeviceId == id && s.Status == ShiftStatus.Open, ct))
                {
                    return "A shift is open on this device. Close it first.";
                }

                if (delete)
                {
                    var status = await db.Devices.Where(d => d.Id == id).Select(d => d.Status).FirstAsync(ct);
                    if (status is DeviceStatus.Active or DeviceStatus.Pending)
                    {
                        return "Make the device Inactive before deleting it.";
                    }
                }

                break;
            case LifecycleKind.Customer:
                if (delete && await db.CustomerCreditLedgers.AnyAsync(c => c.Id == id && c.Balance > 0, ct))
                {
                    return "This customer still owes a balance. Collect it before deleting.";
                }

                break;
            default:
                break;
        }

        return null;
    }

    // Anything that held a session as this record must stop working at once, not at the next token refresh.
    private async Task AfterDeactivateAsync(LifecycleKind kind, Guid id, CancellationToken ct)
    {
        if (kind == LifecycleKind.Staff)
        {
            await refreshTokenService.RevokeAllForMembershipAsync(id, ct);
        }
        else if (kind == LifecycleKind.Device)
        {
            var device = await db.Devices.FirstAsync(d => d.Id == id, ct);
            var now = DateTimeOffset.UtcNow;
            foreach (var credential in await db.DeviceCredentials.Where(c => c.DeviceId == id && c.RevokedAt == null).ToListAsync(ct))
            {
                credential.RevokedAt = now;
            }

            device.SessionVersion++;
            await refreshTokenService.RevokeAllForDeviceAsync(id, ct);
        }
    }

    private async Task<LifecycleResultDto> FinishAsync(LifecycleKind kind, Guid id, Target target, LifecycleStatus before, AuditActionType action, CancellationToken ct)
    {
        auditLogRepository.Add(new AuditLog
        {
            TenantId = tenantProvider.TenantId ?? Guid.Empty,
            ActorUserId = actorProvider.UserId ?? Guid.Empty,
            ActionType = action,
            TargetEntityType = kind.ToString(),
            TargetEntityId = id,
            BeforeStateJson = JsonSerializer.Serialize(new { name = target.Name, status = before.ToString() }),
            AfterStateJson = JsonSerializer.Serialize(new { name = target.Name, status = target.Status.ToString() }),
        });
        _ = await unitOfWork.SaveChangesAsync(ct);
        return new LifecycleResultDto(id, target.Name, target.Status);
    }
}
