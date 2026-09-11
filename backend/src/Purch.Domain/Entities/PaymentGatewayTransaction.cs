using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>QR Ph only (Xendit) — manual Bank Transfer confirmation never touches this table.</summary>
public class PaymentGatewayTransaction : TenantScopedEntity
{
    public Guid PaymentId { get; set; }

    public string XenditInvoiceId { get; set; } = string.Empty;

    public string StatusFromGateway { get; set; } = string.Empty;

    public DateTimeOffset? WebhookReceivedAt { get; set; }

    public string? RawPayloadJson { get; set; }
}
