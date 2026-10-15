namespace Purch.Application.Inventory.Import;

/// <summary>CSV column names shared by the inventory export and import. Exported files append <see cref="ReadOnly"/>
/// context columns after these; import ignores them.</summary>
public static class ImportColumns
{
    public static readonly string[] Catalog =
        ["type", "sku", "barcode", "name", "category", "price", "cost", "reorder_threshold", "unit", "active"];

    public static readonly string[] Stock = ["type", "sku", "barcode", "name", "counted_qty", "note"];

    public static readonly string[] ReadOnly = ["id", "current_stock", "updated_at"];
}
