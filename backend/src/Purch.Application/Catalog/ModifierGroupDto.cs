namespace Purch.Application.Catalog;

public sealed record ModifierGroupDto(
    Guid Id,
    string Name,
    bool AllowMultipleSelection,
    bool IsRequired,
    IReadOnlyList<ItemModifierDto> Modifiers);

public sealed record ItemModifierDto(Guid Id, string Name, decimal PriceDelta);
