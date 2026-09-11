namespace Purch.Application.Pos;

public sealed record AddTransactionLineRequest(Guid ItemId, Guid? ItemVariantId, decimal Quantity);

public sealed record UpdateTransactionLineRequest(decimal Quantity);
