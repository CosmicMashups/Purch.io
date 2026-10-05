namespace Purch.Application.Catalog;

public sealed record ItemComboComponentDto(
    Guid Id,
    Guid ComponentCategoryId,
    string ComponentCategoryName,
    string SlotLabel,
    int Quantity,
    decimal? SubstitutionUpchargeAmount,
    Guid? ComponentItemId = null,
    string? ComponentItemName = null,
    IReadOnlyList<ComboChoiceUpchargeDto>? ChoiceUpcharges = null);

/// <summary>An extra charge for one specific choice within a "choose" slot, e.g. Milk Tea +₱20 in a drink slot.</summary>
public sealed record ComboChoiceUpchargeDto(Guid ItemId, decimal Amount);

/// <summary>e.g. a "Value Meal" combo's "Choose a Drink" slot: ComponentCategoryId points at the Drinks category,
/// Quantity 1, SubstitutionUpchargeAmount set only if picking outside the included tier costs extra.
/// A fixed slot (a specific item, not a choice) sends ComponentItemId instead; ComponentCategoryId may then be
/// Guid.Empty and the server uses that item's category. ChoiceUpcharges prices individual choices in a choose slot.</summary>
public sealed record CreateItemComboComponentRequest(
    Guid ComponentCategoryId,
    string SlotLabel,
    int Quantity,
    decimal? SubstitutionUpchargeAmount,
    Guid? ComponentItemId = null,
    IReadOnlyList<ComboChoiceUpchargeDto>? ChoiceUpcharges = null);

/// <summary>Replaces a slot's definition. The same shape as creating one.</summary>
public sealed record UpdateItemComboComponentRequest(
    Guid ComponentCategoryId,
    string SlotLabel,
    int Quantity,
    decimal? SubstitutionUpchargeAmount,
    Guid? ComponentItemId = null,
    IReadOnlyList<ComboChoiceUpchargeDto>? ChoiceUpcharges = null);
