namespace Purch.Application.Catalog;

public sealed record CreateModifierGroupRequest(string Name, bool AllowMultipleSelection, bool IsRequired);

public sealed record CreateItemModifierRequest(string Name, decimal PriceDelta);
