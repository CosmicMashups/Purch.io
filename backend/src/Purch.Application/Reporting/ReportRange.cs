using Purch.Application.Common.Exceptions;

namespace Purch.Application.Reporting;

/// <summary>Range guard shared by the aggregate reports: a multi-year scan over transactions and lines can time
/// out on a serverless instance, so it is refused rather than attempted.</summary>
public static class ReportRange
{
    public const int MaxDays = 366;

    public static void Validate(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (toUtc <= fromUtc)
        {
            throw new ValidationException("range", "The end of the range must be after its start.");
        }

        if ((toUtc - fromUtc).TotalDays > MaxDays)
        {
            throw new ValidationException("range", $"Choose a range of {MaxDays} days or fewer.");
        }
    }
}
