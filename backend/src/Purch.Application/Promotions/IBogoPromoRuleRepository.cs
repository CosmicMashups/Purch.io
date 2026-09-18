using Purch.Domain.Entities;

namespace Purch.Application.Promotions;

public interface IBogoPromoRuleRepository
{
    Task<IReadOnlyList<BogoPromoRule>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Batch-loads every rule currently in its active window, for TransactionService
    /// to call once per RecalculateTotalAsync rather than re-querying per rule check.</summary>
    Task<IReadOnlyList<BogoPromoRule>> ListActiveByTenantAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<BogoPromoRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(BogoPromoRule rule);
}
