using Purch.Domain.Enums;

namespace Purch.Application.Approvals;

/// <summary>Groups a day's void/refund/kitchen-order-edit approvals by who approved them and calls out
/// patterns worth a manager's second look. Pure — no database, no clock of its own — so the thresholds
/// below can be tested directly. See TransactionService.ApproverRoles and ApproverAuthorizationService
/// for where these approvals actually happen.</summary>
public static class ApprovalsReviewCalculator
{
    /// <summary>One approval, exactly as recorded: who asked, who approved, what and when. A record, not
    /// the AuditLog entity itself, so this class never needs EF Core to be testable.</summary>
    public sealed record Record(
        Guid ApproverId,
        Guid RequesterId,
        AuditActionType ActionType,
        Guid TargetEntityId,
        DateTimeOffset CreatedAt);

    public sealed record UserInfo(string Name, Role Role);

    /// <summary>Outside this local-time window, an approval counts as after-hours. Fixed rather than
    /// per-branch: nothing in this codebase tracks a branch's operating hours yet, and a wide 6am-10pm
    /// window flags only genuinely unusual timing (a 2am void) without also flagging an ordinary late
    /// closing shift.</summary>
    private static readonly TimeSpan AfterHoursStart = TimeSpan.FromHours(22);
    private static readonly TimeSpan AfterHoursEnd = TimeSpan.FromHours(6);

    /// <summary>An approver with at least this many approvals in one day is flagged, regardless of who
    /// asked or when — the volume alone is worth a look.</summary>
    private const int ManyApprovalsThreshold = 6;

    /// <summary>An approver with at least this many after-hours approvals in one day is flagged.</summary>
    private const int ManyAfterHoursThreshold = 2;

    /// <summary>One requester needs at least this many approvals from the same approver, AND at least this
    /// share of that approver's day, before it's flagged as one-sided rather than ordinary — a single
    /// cashier's normal cart of returns on a slow day shouldn't trip this.</summary>
    private const int DominantRequesterMinCount = 3;
    private const double DominantRequesterMinShare = 0.6;

    public static ApprovalsReviewDto Build(
        DateOnly date,
        TimeSpan localOffset,
        IReadOnlyList<Record> records,
        IReadOnlyDictionary<Guid, UserInfo> usersById)
    {
        string NameOf(Guid userId) => usersById.TryGetValue(userId, out var user) ? user.Name : "(former staff)";
        bool IsAfterHours(DateTimeOffset createdAt)
        {
            var localTime = createdAt.ToOffset(localOffset).TimeOfDay;
            return localTime >= AfterHoursStart || localTime < AfterHoursEnd;
        }

        var approvers = records
            .GroupBy(record => record.ApproverId)
            .Select(group =>
            {
                var entries = group
                    .OrderBy(record => record.CreatedAt)
                    .Select(record => new ApprovalEntryDto(
                        record.RequesterId,
                        NameOf(record.RequesterId),
                        record.ActionType,
                        record.TargetEntityId,
                        record.CreatedAt,
                        IsAfterHours(record.CreatedAt)))
                    .ToList();

                var afterHoursCount = entries.Count(entry => entry.AfterHours);
                var flags = new List<ApprovalFlagDto>();

                if (entries.Count >= ManyApprovalsThreshold)
                {
                    flags.Add(new ApprovalFlagDto($"Approved {entries.Count} times today — unusually high for one person."));
                }

                if (afterHoursCount >= ManyAfterHoursThreshold)
                {
                    flags.Add(new ApprovalFlagDto($"{afterHoursCount} of these were approved outside normal hours (10pm-6am)."));
                }

                var byRequester = entries.GroupBy(entry => entry.RequesterId).OrderByDescending(g => g.Count()).First();
                if (byRequester.Count() >= DominantRequesterMinCount && byRequester.Count() >= entries.Count * DominantRequesterMinShare)
                {
                    flags.Add(new ApprovalFlagDto($"{byRequester.Count()} of {entries.Count} were all asked by {NameOf(byRequester.Key)}."));
                }

                var approverInfo = usersById.GetValueOrDefault(group.Key);
                return new ApproverSummaryDto(
                    group.Key,
                    approverInfo?.Name ?? "(former staff)",
                    approverInfo?.Role ?? Role.Manager,
                    entries.Count,
                    afterHoursCount,
                    flags,
                    entries);
            })
            .OrderByDescending(approver => approver.TotalApprovals)
            .ToList();

        return new ApprovalsReviewDto(date, records.Count, approvers);
    }
}
