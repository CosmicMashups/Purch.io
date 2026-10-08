namespace Purch.Application.Common.Exceptions;

/// <summary>Too many attempts for this account for now — maps to 429 with a Retry-After header.</summary>
public sealed class TooManyRequestsException(string message, TimeSpan retryAfter) : AppException(message)
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
