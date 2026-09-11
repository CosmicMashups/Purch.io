using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed record CreateStaffRequest(
    string Name,
    Role Role,
    ScopeType ScopeType,
    Guid? ScopeId,
    Guid? BranchId,
    string Pin);
