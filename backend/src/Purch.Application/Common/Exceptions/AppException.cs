namespace Purch.Application.Common.Exceptions;

/// <summary>
/// Base for exceptions that carry an intentional, user-meaningful error — as opposed to
/// a truly unexpected failure (a bug, a dead database). GlobalExceptionHandler maps each
/// subtype below to a specific HTTP status code and a description safe to show the caller.
/// Use these from any Application-layer service; never construct this base type directly.
/// </summary>
public abstract class AppException(string message) : Exception(message);
