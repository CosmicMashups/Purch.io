using Microsoft.EntityFrameworkCore;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfAuditLogRepository(PurchDbContext dbContext) : IAuditLogRepository
{
    public async Task<IReadOnlyList<AuditLog>> QueryAsync(Guid tenantId, AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var logs = dbContext.AuditLogs.AsNoTracking().Where(log => log.TenantId == tenantId);

        if (query.ActorUserId is { } actorUserId)
        {
            logs = logs.Where(log => log.ActorUserId == actorUserId);
        }

        if (query.ActionType is { } actionType)
        {
            logs = logs.Where(log => log.ActionType == actionType);
        }

        if (query.From is { } from)
        {
            logs = logs.Where(log => log.CreatedAt >= from);
        }

        if (query.To is { } to)
        {
            logs = logs.Where(log => log.CreatedAt <= to);
        }

        return await logs.OrderByDescending(log => log.CreatedAt).ToListAsync(cancellationToken);
    }
}
