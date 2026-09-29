namespace Purch.Application.Approvals;

public interface IApprovalsReviewService
{
    /// <summary>The current tenant's approvals for the given local day (default: today). See
    /// ApprovalsReviewCalculator for how patterns are flagged.</summary>
    Task<ApprovalsReviewDto> GetDailyReviewAsync(DateOnly? forDate = null, CancellationToken cancellationToken = default);
}
