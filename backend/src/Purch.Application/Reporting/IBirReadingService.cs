namespace Purch.Application.Reporting;

public interface IBirReadingService
{
    /// <summary>Read-only mid-shift snapshot — can be run any number of times, never advances the reset counter.</summary>
    Task<BirReadingDto> GenerateXReadingAsync(CancellationToken cancellationToken = default);

    /// <summary>End-of-day close — advances the reset counter and rolls the covered receipts into GrandAccumulatedSales, so the next reading starts fresh from here.</summary>
    Task<BirReadingDto> GenerateZReadingAsync(CancellationToken cancellationToken = default);
}
