namespace Purch.Application.Catalog;

public sealed record ItemBatchDto(
    Guid Id,
    string LotNumber,
    DateOnly? ExpiryDate,
    decimal QuantityReceived,
    decimal QuantityRemaining,
    DateTimeOffset ReceivedAt);

/// <summary>Receiving a batch is how weight/volume stock enters the system — see ItemBatchService.</summary>
public sealed record CreateItemBatchRequest(string LotNumber, DateOnly? ExpiryDate, decimal QuantityReceived);
