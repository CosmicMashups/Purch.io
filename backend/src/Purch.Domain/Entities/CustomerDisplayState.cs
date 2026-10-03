using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>The last thing a Register showed its customer display: the cart, the totals, the thank-you. One row per Register,
/// replaced on every change. A paired customer display polls for it, since a serverless API cannot hold a connection open
/// or keep it in memory between requests. Holds only what the customer may see.</summary>
public class CustomerDisplayState : TenantScopedEntity
{
    public Guid RegisterDeviceId { get; set; }

    /// <summary>The display payload as JSON, exactly as the till built it. The server does not interpret it.</summary>
    public string StateJson { get; set; } = "null";

    /// <summary>Goes up by one on every change, so a display can ask "anything newer than the one I have?" cheaply.</summary>
    public long Version { get; set; }
}
