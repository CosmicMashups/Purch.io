using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed record StaffDto(
    Guid Id,
    string Name,
    Role Role,
    ScopeType ScopeType,
    Guid? ScopeId,
    Guid? BranchId,
    bool IsActive);
