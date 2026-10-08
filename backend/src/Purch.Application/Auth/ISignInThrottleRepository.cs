namespace Purch.Application.Auth;

/// <summary>What is recorded for one email: null means no failures are on record.</summary>
public sealed record SignInThrottleState(DateTimeOffset? BlockedUntil);

public interface ISignInThrottleRepository
{
    Task<SignInThrottleState?> GetAsync(string emailHash, CancellationToken cancellationToken = default);

    /// <summary>Counts a wrong password (atomically, so parallel guesses all count) and starts or extends the block.</summary>
    Task RecordFailureAsync(string emailHash, CancellationToken cancellationToken = default);

    /// <summary>A correct password clears the history.</summary>
    Task ClearAsync(string emailHash, CancellationToken cancellationToken = default);
}
