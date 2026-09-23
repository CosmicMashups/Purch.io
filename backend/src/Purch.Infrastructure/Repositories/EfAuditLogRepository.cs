using Microsoft.EntityFrameworkCore;
using Purch.Application.Common;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfAuditLogRepository(PurchDbContext dbContext) : IAuditLogRepository
{
    public void Add(AuditLog log)
    {
        _ = dbContext.AuditLogs.Add(log);
    }

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

        if (query.Before is { } before)
        {
            if (query.BeforeId is { } beforeId)
            {
                logs = logs.Where(log => log.CreatedAt < before || (log.CreatedAt == before && log.Id.CompareTo(beforeId) < 0));
            }
            else
            {
                logs = logs.Where(log => log.CreatedAt < before);
            }
        }

        return await logs
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(Paging.ClampLimit(query.Limit))
            .ToListAsync(cancellationToken);
    }
}
