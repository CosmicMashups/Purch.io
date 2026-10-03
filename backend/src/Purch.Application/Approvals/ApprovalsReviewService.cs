using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Onboarding;
using Purch.Application.Reporting;

namespace Purch.Application.Approvals;

public sealed class ApprovalsReviewService(
    IAuditLogRepository auditLogRepository,
    IUserRepository userRepository,
    ICurrentTenantProvider currentTenantProvider) : IApprovalsReviewService
{
    public async Task<ApprovalsReviewDto> GetDailyReviewAsync(DateOnly? forDate = null, CancellationToken cancellationToken = default)
    {
        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("The approvals review requires an authenticated tenant context.");

        var day = forDate ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(ReportTimeZone.Offset).Date);
        var from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), ReportTimeZone.Offset).ToUniversalTime();
        var to = from.AddDays(1);

        var logs = await auditLogRepository.ListApprovalsAsync(tenantId, from, to, cancellationToken);
        var records = logs
            .Where(log => log.ApprovedByUserId is not null)
            .Select(log => new ApprovalsReviewCalculator.Record(log.ApprovedByUserId!.Value, log.ActorUserId, log.ActionType, log.TargetEntityId, log.CreatedAt))
            .ToList();

        // Every staff member, active or not — an approval by someone since deactivated should still show
        // their name, not "(former staff)" for no reason.
        var users = await userRepository.ListActorsAsync(tenantId, cancellationToken);
        var usersById = users.ToDictionary(user => user.Id, user => new ApprovalsReviewCalculator.UserInfo(user.Name, user.Role));

        return ApprovalsReviewCalculator.Build(day, ReportTimeZone.Offset, records, usersById);
    }
}
