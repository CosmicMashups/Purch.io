namespace Purch.Application.Common.Exceptions;

/// <summary>The requested resource doesn't exist (or isn't visible to this tenant) — maps to 404 Not Found.</summary>
public sealed class NotFoundException(string resourceName, object key)
    : AppException($"{resourceName} '{key}' was not found.")
{
    /// <summary>What was missing (e.g. "Open cart"); sent as problem-details "resource".</summary>
    public string Resource { get; } = resourceName;
}
