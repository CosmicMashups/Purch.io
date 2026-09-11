using Purch.Application.Common;

namespace Purch.Common.TestUtilities;

/// <summary>Settable ICurrentTenantProvider for tests — swap TenantId between assertions.</summary>
public sealed class TestCurrentTenantProvider : ICurrentTenantProvider
{
    public Guid? TenantId { get; set; }
}
