using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed record BranchDto(
    Guid Id,
    string Name,
    string? Address,
    ReceiptPrinterProfile ReceiptPrinterProfile,
    bool CashDrawerEnabled,
    CashDrawerPolicy CashDrawerPolicy);
