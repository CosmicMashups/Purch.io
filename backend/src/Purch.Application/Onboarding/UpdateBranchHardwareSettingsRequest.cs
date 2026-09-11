using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed record UpdateBranchHardwareSettingsRequest(
    ReceiptPrinterProfile ReceiptPrinterProfile,
    bool CashDrawerEnabled,
    CashDrawerPolicy CashDrawerPolicy);
