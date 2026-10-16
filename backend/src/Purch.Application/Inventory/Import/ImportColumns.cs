namespace Purch.Application.Inventory.Import;

/// <summary>CSV column names shared by the inventory export and import. Exported files append <see cref="ReadOnly"/>
/// context columns after these; import ignores them.
/// Contract for import: text columns sku/barcode/name/category/unit may be exported with ONE leading <c>'</c> guard
/// when the value starts with = + - @ tab or CR; the importer must strip exactly one leading <c>'</c> iff the next
/// char is one of those, before matching. Barcodes are compared as text, and spreadsheet apps may strip leading zeros.</summary>
public static class ImportColumns
{
    public static readonly string[] Catalog =
        ["type", "sku", "barcode", "name", "category", "price", "cost", "reorder_threshold", "unit", "active"];

    /// <summary>Accepted by a catalog import but never exported: the opening stock of a NEW row, posted as a StockIn.
    /// Ignored for rows that match an existing record (stock is only ever changed by a stock import's delta).</summary>
    public static readonly string[] CatalogImportOnly = ["opening_stock"];

    public static readonly string[] Stock = ["type", "sku", "barcode", "name", "counted_qty", "note"];

    public static readonly string[] ReadOnly = ["id", "current_stock", "updated_at"];
}
