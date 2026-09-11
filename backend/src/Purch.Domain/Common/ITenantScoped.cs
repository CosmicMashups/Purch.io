namespace Purch.Domain.Common;

/// <summary>
/// Marks an entity as tenant-scoped. PurchDbContext applies a global query filter
/// to every entity implementing this — the single enforcement point for
/// shared-database multi-tenant isolation (NFR14).
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
