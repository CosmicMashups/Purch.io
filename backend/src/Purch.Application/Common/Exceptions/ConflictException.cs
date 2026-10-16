namespace Purch.Application.Common.Exceptions;

/// <summary>The request conflicts with existing state (e.g. a duplicate that must be unique) — maps to 409 Conflict.</summary>
public sealed class ConflictException(string message, string? code = null) : AppException(message)
{
    public override string? Code { get; init; } = code;
}
