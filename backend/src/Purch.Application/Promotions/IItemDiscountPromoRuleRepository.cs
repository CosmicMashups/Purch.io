using Purch.Domain.Entities;

namespace Purch.Application.Promotions;

public interface IItemDiscountPromoRuleRepository
{
    Task<IReadOnlyList<ItemDiscountPromoRule>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Batch-loads every rule currently in its active window, for TransactionService
    /// to call once per RecalculateTotalAsync rather than re-querying per rule check.</summary>
    Task<IReadOnlyList<ItemDiscountPromoRule>> ListActiveByTenantAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<ItemDiscountPromoRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(ItemDiscountPromoRule rule);
}
