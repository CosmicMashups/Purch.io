using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class Item : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public Guid? CategoryId { get; set; }

    public decimal BasePrice { get; set; }

    public string? ImageUrl { get; set; }

    public PricingType PricingType { get; set; }

    public decimal StockOnHand { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? DepartmentId { get; set; }
}
