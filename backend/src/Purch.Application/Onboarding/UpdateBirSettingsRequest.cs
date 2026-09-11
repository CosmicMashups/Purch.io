namespace Purch.Application.Onboarding;

public sealed record UpdateBirSettingsRequest(
    string? Tin,
    string? RegisteredBusinessName,
    string? RegisteredAddress,
    int? CreditLedgerRetentionDays);
