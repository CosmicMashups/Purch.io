using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>Weight/volume mode: batch/lot + expiry, FIFO stock-out.</summary>
public class ItemBatch : TenantScopedEntity
{
    public Guid ItemId { get; set; }

    public string LotNumber { get; set; } = string.Empty;

    public DateOnly? ExpiryDate { get; set; }

    public decimal QuantityReceived { get; set; }

    public decimal QuantityRemaining { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
