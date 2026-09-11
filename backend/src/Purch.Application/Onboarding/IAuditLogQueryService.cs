namespace Purch.Application.Onboarding;

public interface IAuditLogQueryService
{
    Task<IReadOnlyList<AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}
