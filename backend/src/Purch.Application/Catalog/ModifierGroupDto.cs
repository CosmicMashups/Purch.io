namespace Purch.Application.Catalog;

public sealed record ModifierGroupDto(
    Guid Id,
    string Name,
    bool AllowMultipleSelection,
    IReadOnlyList<ItemModifierDto> Modifiers);

public sealed record ItemModifierDto(Guid Id, string Name, decimal PriceDelta);
