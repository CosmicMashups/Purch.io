import '../../catalog/domain/category_models.dart';
import '../../catalog/domain/item_models.dart';
import 'purchase_order_models.dart';

/// One row of the Inventory tab's items table: what it is, what's on hand,
/// and what it costs us.
class ItemStockCostRow {
  const ItemStockCostRow({
    required this.itemId,
    required this.itemName,
    required this.categoryName,
    required this.availableStock,
    required this.averageCost,
    required this.costSampleCount,
  });

  final String itemId;
  final String itemName;

  /// "Uncategorized" when the item has no category assigned — never blank,
  /// so the column never reads as missing data.
  final String categoryName;

  final double availableStock;

  /// Null when no received purchase-order line has ever quoted a cost for
  /// this item. The table shows an explicit "—  no receipts yet" rather than
  /// ₱0.00, which would read as "this is free".
  final double? averageCost;

  /// How many received PO lines the average is drawn from — shown in the
  /// tooltip so a one-sample average isn't mistaken for a settled figure.
  final int costSampleCount;
}

/// Quantity-weighted mean of `expectedUnitCost` across purchase-order lines
/// that have actually been received, per item.
///
/// Approach and its limits, since this is a client-side aggregate and not a
/// server-computed one:
/// - Only `received` and `partiallyReceived` orders count. A draft, sent or
///   cancelled PO quotes a cost that was never paid.
/// - Within those orders, only lines with `quantityReceived > 0` count, and
///   each line is weighted by `quantityReceived` — receiving 100 units at
///   ₱10 and 1 unit at ₱50 should average near ₱10, not ₱30.
/// - `expectedUnitCost` is the *expected* cost; the API carries no actual
///   landed cost, so this is the best available proxy.
///
/// It runs over the PO list already loaded for the Purchase Orders screen, so
/// it costs no extra request. If PO volume ever outgrows a full client-side
/// list, this becomes a backend aggregation — the row model stays the same.
Map<String, ({double averageCost, int sampleCount})> aggregateAverageUnitCost(
  List<PurchaseOrder> purchaseOrders,
) {
  final weightedTotal = <String, double>{};
  final quantityTotal = <String, double>{};
  final sampleCount = <String, int>{};

  for (final order in purchaseOrders) {
    final isReceived =
        order.status == PurchaseOrderStatus.received ||
        order.status == PurchaseOrderStatus.partiallyReceived;
    if (!isReceived) {
      continue;
    }

    for (final line in order.lines) {
      if (line.quantityReceived <= 0) {
        continue;
      }
      weightedTotal[line.itemId] =
          (weightedTotal[line.itemId] ?? 0) +
          line.expectedUnitCost * line.quantityReceived;
      quantityTotal[line.itemId] =
          (quantityTotal[line.itemId] ?? 0) + line.quantityReceived;
      sampleCount[line.itemId] = (sampleCount[line.itemId] ?? 0) + 1;
    }
  }

  return {
    for (final entry in quantityTotal.entries)
      if (entry.value > 0)
        entry.key: (
          averageCost: weightedTotal[entry.key]! / entry.value,
          sampleCount: sampleCount[entry.key] ?? 0,
        ),
  };
}

/// Builds the Inventory items table from the catalog plus the PO history.
List<ItemStockCostRow> buildItemStockCostRows({
  required List<Item> items,
  required List<Category> categories,
  required List<PurchaseOrder> purchaseOrders,
}) {
  final categoryNameById = <String, String>{
    for (final category in categories) category.id: category.name,
  };
  final costs = aggregateAverageUnitCost(purchaseOrders);

  final rows = [
    for (final item in items)
      ItemStockCostRow(
        itemId: item.id,
        itemName: item.name,
        categoryName:
            item.categoryId == null
                ? 'Uncategorized'
                : categoryNameById[item.categoryId] ?? 'Uncategorized',
        availableStock: item.stockOnHand,
        averageCost: costs[item.id]?.averageCost,
        costSampleCount: costs[item.id]?.sampleCount ?? 0,
      ),
  ];

  rows.sort(
    (a, b) => a.itemName.toLowerCase().compareTo(b.itemName.toLowerCase()),
  );
  return rows;
}
