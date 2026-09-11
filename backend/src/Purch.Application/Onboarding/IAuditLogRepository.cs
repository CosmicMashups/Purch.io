using Purch.Domain.Entities;

namespace Purch.Application.Onboarding;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> QueryAsync(Guid tenantId, AuditLogQuery query, CancellationToken cancellationToken = default);
}
