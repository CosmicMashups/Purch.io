namespace Purch.Application.Onboarding;

public sealed record CreateBranchRequest(string Name, string? Address, string? Code = null);

public sealed record UpdateBranchDetailsRequest(string Name, string? Address, string Code);
