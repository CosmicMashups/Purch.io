using Purch.Application.Approvals;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Approvals;

public sealed class ApprovalsReviewCalculatorTests
{
    private static readonly TimeSpan Ph = TimeSpan.FromHours(8);
    private static readonly Guid Manager = Guid.Parse("00000000-0000-0000-0000-0000000000A1");
    private static readonly Guid OtherManager = Guid.Parse("00000000-0000-0000-0000-0000000000A2");
    private static readonly Guid CashierA = Guid.Parse("00000000-0000-0000-0000-0000000000B1");
    private static readonly Guid CashierB = Guid.Parse("00000000-0000-0000-0000-0000000000B2");

    private static readonly IReadOnlyDictionary<Guid, ApprovalsReviewCalculator.UserInfo> Users =
        new Dictionary<Guid, ApprovalsReviewCalculator.UserInfo>
        {
            [Manager] = new("Mae Manager", Role.Manager),
            [OtherManager] = new("Aida Admin", Role.Admin),
            [CashierA] = new("Cal Cashier", Role.Cashier),
            [CashierB] = new("Bea Bagger", Role.Cashier),
        };

    /// <summary>A moment at the given hour, Philippine local time, on the report day. Hours past midnight
    /// (e.g. 2am) land on the next calendar day, matching how an overnight shift's timestamp actually reads.</summary>
    private static DateTimeOffset At(int hourPhLocal, int minute = 0) =>
        new DateTimeOffset(2026, 9, 29, 0, 0, 0, Ph).AddHours(hourPhLocal).AddMinutes(minute);

    private static ApprovalsReviewCalculator.Record Approval(Guid approver, Guid requester, DateTimeOffset at, AuditActionType action = AuditActionType.Void, Guid? target = null) =>
        new(approver, requester, action, target ?? Guid.NewGuid(), at);

    [Fact]
    public void No_approvals_yields_an_empty_review()
    {
        var review = ApprovalsReviewCalculator.Build(new DateOnly(2026, 9, 29), Ph, [], Users);

        Assert.Equal(0, review.TotalApprovals);
        Assert.Empty(review.Approvers);
    }

    [Fact]
    public void Groups_approvals_by_approver_and_names_them()
    {
        var records = new[]
        {
            Approval(Manager, CashierA, At(10)),
            Approval(Manager, CashierB, At(11)),
            Approval(OtherManager, CashierA, At(12)),
        };

        var review = ApprovalsReviewCalculator.Build(new DateOnly(2026, 9, 29), Ph, records, Users);

        Assert.Equal(3, review.TotalApprovals);
        Assert.Equal(2, review.Approvers.Count);
        var mae = review.Approvers.Single(a => a.ApproverId == Manager);
        Assert.Equal("Mae Manager", mae.ApproverName);
        Assert.Equal(Role.Manager, mae.ApproverRole);
        Assert.Equal(2, mae.TotalApprovals);
        Assert.Equal("Cal Cashier", mae.Entries[0].RequesterName);
    }

    [Fact]
    public void Flags_an_approver_with_many_approvals_in_one_day()
    {
        var records = Enumerable.Range(0, 6).Select(i => Approval(Manager, CashierA, At(9 + i))).ToArray();

        var review = ApprovalsReviewCalculator.Build(new DateOnly(2026, 9, 29), Ph, records, Users);

        var mae = review.Approvers.Single();
        Assert.Contains(mae.Flags, f => f.Message.Contains("unusually high", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Does_not_flag_a_normal_number_of_approvals_spread_across_requesters()
    {
        var records = new[]
        {
            Approval(Manager, CashierA, At(9)),
            Approval(Manager, CashierB, At(10)),
            Approval(Manager, CashierA, At(11)),
        };

        var review = ApprovalsReviewCalculator.Build(new DateOnly(2026, 9, 29), Ph, records, Users);

        Assert.Empty(review.Approvers.Single().Flags);
    }

    [Theory]
    [InlineData(23, true)] // 11pm
    [InlineData(2, true)] // 2am
    [InlineData(9, false)] // 9am
    [InlineData(21, false)] // 9pm, still inside the window
    public void Marks_entries_outside_10pm_to_6am_as_after_hours(int hour, bool expectedAfterHours)
    {
        var records = new[] { Approval(Manager, CashierA, At(hour)) };

        var review = ApprovalsReviewCalculator.Build(new DateOnly(2026, 9, 29), Ph, records, Users);

        Assert.Equal(expectedAfterHours, review.Approvers.Single().Entries.Single().AfterHours);
    }

    [Fact]
    public void Flags_an_approver_with_several_after_hours_approvals()
    {
        var records = new[]
        {
            Approval(Manager, CashierA, At(23)),
            Approval(Manager, CashierB, At(1)),
            Approval(Manager, CashierA, At(10)),
        };

        var review = ApprovalsReviewCalculator.Build(new DateOnly(2026, 9, 29), Ph, records, Users);

        var mae = review.Approvers.Single();
        Assert.Equal(2, mae.AfterHoursApprovals);
        Assert.Contains(mae.Flags, f => f.Message.Contains("outside normal hours"));
    }

    [Fact]
    public void Flags_one_requester_dominating_an_approvers_day()
    {
        var records = new[]
        {
            Approval(Manager, CashierA, At(9)),
            Approval(Manager, CashierA, At(10)),
            Approval(Manager, CashierA, At(11)),
            Approval(Manager, CashierB, At(12)),
        };

        var review = ApprovalsReviewCalculator.Build(new DateOnly(2026, 9, 29), Ph, records, Users);

        var mae = review.Approvers.Single();
        Assert.Contains(mae.Flags, f => f.Message.Contains("Cal Cashier"));
    }

    [Fact]
    public void Does_not_flag_an_even_split_between_requesters()
    {
        var records = new[]
        {
            Approval(Manager, CashierA, At(9)),
            Approval(Manager, CashierA, At(10)),
            Approval(Manager, CashierB, At(11)),
            Approval(Manager, CashierB, At(12)),
        };

        var review = ApprovalsReviewCalculator.Build(new DateOnly(2026, 9, 29), Ph, records, Users);

        Assert.Empty(review.Approvers.Single().Flags);
    }

    [Fact]
    public void A_deactivated_or_deleted_staff_member_still_shows_as_former_staff()
    {
        var unknown = Guid.NewGuid();
        var records = new[] { Approval(unknown, CashierA, At(9)) };

        var review = ApprovalsReviewCalculator.Build(new DateOnly(2026, 9, 29), Ph, records, Users);

        Assert.Equal("(former staff)", review.Approvers.Single().ApproverName);
    }

    [Fact]
    public void Approvers_are_ordered_by_how_many_approvals_they_gave()
    {
        var records = new[]
        {
            Approval(OtherManager, CashierA, At(9)),
            Approval(Manager, CashierA, At(9)),
            Approval(Manager, CashierB, At(10)),
        };

        var review = ApprovalsReviewCalculator.Build(new DateOnly(2026, 9, 29), Ph, records, Users);

        Assert.Equal(Manager, review.Approvers[0].ApproverId);
        Assert.Equal(OtherManager, review.Approvers[1].ApproverId);
    }
}
