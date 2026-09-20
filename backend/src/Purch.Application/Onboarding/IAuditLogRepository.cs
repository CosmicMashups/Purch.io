using Purch.Domain.Entities;

namespace Purch.Application.Onboarding;

public interface IAuditLogRepository
{
    /// <summary>Stages an audit entry - call IUnitOfWork.SaveChangesAsync to commit it with the change it records.</summary>
    void Add(AuditLog log);

    Task<IReadOnlyList<AuditLog>> QueryAsync(Guid tenantId, AuditLogQuery query, CancellationToken cancellationToken = default);
}
