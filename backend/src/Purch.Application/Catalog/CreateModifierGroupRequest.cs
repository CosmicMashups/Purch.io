namespace Purch.Application.Catalog;

public sealed record CreateModifierGroupRequest(string Name, bool AllowMultipleSelection);

public sealed record CreateItemModifierRequest(string Name, decimal PriceDelta);
