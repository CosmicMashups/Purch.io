namespace Purch.Application.Common;

/// <summary>
/// Supplies the ambient tenant id for the current request, resolved by
/// TenantResolutionMiddleware from the JWT tenant claim. PurchDbContext's
/// global query filter reads this — the single point every tenant-scoped
/// query is filtered through (NFR14: no cross-tenant leakage).
/// </summary>
public interface ICurrentTenantProvider
{
    Guid? TenantId { get; }
}
