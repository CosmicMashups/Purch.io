using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

/// <summary>PIN reset is deliberately not included here — that's a separate, more sensitive action to build later.</summary>
public sealed record UpdateStaffRequest(
    Role Role,
    ScopeType ScopeType,
    Guid? ScopeId,
    Guid? BranchId,
    bool IsActive);
