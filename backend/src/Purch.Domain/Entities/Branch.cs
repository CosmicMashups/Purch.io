using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Branch : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public ReceiptPrinterProfile ReceiptPrinterProfile { get; set; } = ReceiptPrinterProfile.None;

    public bool CashDrawerEnabled { get; set; }

    public CashDrawerPolicy CashDrawerPolicy { get; set; } = CashDrawerPolicy.KickOnSaleOnly;
}
