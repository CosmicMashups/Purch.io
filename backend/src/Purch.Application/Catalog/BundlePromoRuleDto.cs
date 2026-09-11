namespace Purch.Application.Catalog;

public sealed record BundlePromoRuleDto(
    Guid Id,
    string Description,
    int TriggerQuantity,
    decimal BundlePrice,
    bool IsActive);

/// <summary>e.g. "Buy 2 Get 1" -> TriggerQuantity 3, BundlePrice = 2x unit price; "3 for ₱99" -> TriggerQuantity 3, BundlePrice 99.</summary>
public sealed record CreateBundlePromoRuleRequest(string Description, int TriggerQuantity, decimal BundlePrice);
