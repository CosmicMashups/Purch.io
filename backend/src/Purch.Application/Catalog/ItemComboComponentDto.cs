namespace Purch.Application.Catalog;

public sealed record ItemComboComponentDto(
    Guid Id,
    Guid ComponentCategoryId,
    string ComponentCategoryName,
    string SlotLabel,
    int Quantity,
    decimal? SubstitutionUpchargeAmount);

/// <summary>e.g. a "Value Meal" combo's "Choose a Drink" slot: ComponentCategoryId points at the Drinks category, Quantity 1, SubstitutionUpchargeAmount set only if picking outside the included tier costs extra.</summary>
public sealed record CreateItemComboComponentRequest(
    Guid ComponentCategoryId,
    string SlotLabel,
    int Quantity,
    decimal? SubstitutionUpchargeAmount);
