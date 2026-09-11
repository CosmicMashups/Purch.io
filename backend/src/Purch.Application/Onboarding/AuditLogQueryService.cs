using Purch.Application.Common;
using Purch.Domain.Entities;

namespace Purch.Application.Onboarding;

public sealed class AuditLogQueryService(
    IAuditLogRepository auditLogRepository,
    ICurrentTenantProvider currentTenantProvider) : IAuditLogQueryService
{
    public async Task<IReadOnlyList<AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("The audit log requires an authenticated tenant context.");

        var logs = await auditLogRepository.QueryAsync(tenantId, query, cancellationToken);
        return [.. logs.Select(ToDto)];
    }

    private static AuditLogDto ToDto(AuditLog log)
    {
        return new(log.Id, log.ActorUserId, log.ActionType, log.TargetEntityType, log.TargetEntityId, log.CreatedAt);
    }
}
