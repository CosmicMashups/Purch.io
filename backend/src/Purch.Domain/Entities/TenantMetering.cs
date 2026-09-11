using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>Schema-only hook, unread by any billing gate in v1 — see docs/adr on licensing removal.</summary>
public class TenantMetering : TenantScopedEntity
{
    public string Period { get; set; } = string.Empty;

    public long TransactionCount { get; set; }
}
