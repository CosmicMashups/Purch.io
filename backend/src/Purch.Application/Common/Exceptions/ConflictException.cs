namespace Purch.Application.Common.Exceptions;

/// <summary>The request conflicts with existing state (e.g. a duplicate that must be unique) — maps to 409 Conflict.</summary>
public sealed class ConflictException(string message) : AppException(message);
