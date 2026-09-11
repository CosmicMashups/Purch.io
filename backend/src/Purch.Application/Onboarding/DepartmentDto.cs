namespace Purch.Application.Onboarding;

public sealed record DepartmentDto(Guid Id, Guid BranchId, string Name, string? ConcessionaireContactInfo);

public sealed record CreateDepartmentRequest(string Name, string? ConcessionaireContactInfo);
