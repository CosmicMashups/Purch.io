namespace Purch.Application.Common.Exceptions;

/// <summary>
/// The caller is authenticated but not permitted to do this (wrong Role or out-of-scope
/// Branch/Department per RBAC scoping) — maps to 403 Forbidden. Distinct from a plain 401,
/// which means "we don't know who you are" rather than "we know, and the answer is no."
/// </summary>
public sealed class ForbiddenException(string message) : AppException(message);
