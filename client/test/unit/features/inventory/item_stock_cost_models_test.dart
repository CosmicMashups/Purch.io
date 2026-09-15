import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/inventory/domain/item_stock_cost_models.dart';
import 'package:purch_client/features/inventory/domain/purchase_order_models.dart';

Item _item(String id, String name, {String? categoryId, double stock = 0}) =>
    Item(
      id: id,
      name: name,
      sku: null,
      barcode: null,
      categoryId: categoryId,
      basePrice: 10,
      imageUrl: null,
      pricingType: PricingType.unit,
      stockOnHand: stock,
      isActive: true,
      tingiMode: TingiMode.none,
      packagedSize: null,
      tingiIncrementStep: null,
      tingiAllowedSizes: const [],
      serviceDurationMinutes: null,
      departmentId: null,
      lowStockThreshold: null,
    );

PurchaseOrder _po(
  String id,
  PurchaseOrderStatus status,
  List<PurchaseOrderLine> lines,
) => PurchaseOrder(
  id: id,
  supplierId: 's-1',
  supplierName: 'Supplier',
  branchId: 'b-1',
  branchName: 'Main',
  status: status,
  sentAt: null,
  lines: lines,
);

PurchaseOrderLine _line(
  String id,
  String itemId, {
  required double ordered,
  required double received,
  required double cost,
}) => PurchaseOrderLine(
  id: id,
  itemId: itemId,
  itemName: 'Item $itemId',
  quantityOrdered: ordered,
  quantityReceived: received,
  expectedUnitCost: cost,
);

void main() {
  group('aggregateAverageUnitCost', () {
    test('weights each received line by the quantity received', () {
      final costs = aggregateAverageUnitCost([
        _po(
          'po-1',
          PurchaseOrderStatus.received,
          [_line('l1', 'i1', ordered: 8, received: 8, cost: 12)],
        ),
        _po(
          'po-2',
          PurchaseOrderStatus.partiallyReceived,
          [_line('l2', 'i1', ordered: 10, received: 2, cost: 22)],
        ),
      ]);

      // (8*12 + 2*22) / 10 = 14, not the unweighted mean of 17.
      expect(costs['i1']!.averageCost, closeTo(14, 1e-9));
      expect(costs['i1']!.sampleCount, 2);
    });

    test('ignores draft, sent and cancelled orders', () {
      final costs = aggregateAverageUnitCost([
        _po('po-1', PurchaseOrderStatus.draft, [
          _line('l1', 'i1', ordered: 5, received: 5, cost: 99),
        ]),
        _po('po-2', PurchaseOrderStatus.sent, [
          _line('l2', 'i1', ordered: 5, received: 5, cost: 99),
        ]),
        _po('po-3', PurchaseOrderStatus.cancelled, [
          _line('l3', 'i1', ordered: 5, received: 5, cost: 99),
        ]),
      ]);

      expect(costs, isEmpty);
    });

    test('ignores lines in a received order that received nothing', () {
      final costs = aggregateAverageUnitCost([
        _po('po-1', PurchaseOrderStatus.partiallyReceived, [
          _line('l1', 'i1', ordered: 5, received: 5, cost: 10),
          _line('l2', 'i2', ordered: 5, received: 0, cost: 999),
        ]),
      ]);

      expect(costs['i1']!.averageCost, 10);
      expect(costs.containsKey('i2'), isFalse);
    });
  });

  group('buildItemStockCostRows', () {
    test('joins catalog, categories and costs, sorted by name', () {
      final rows = buildItemStockCostRows(
        items: [
          _item('i2', 'Zucchini', categoryId: 'c1', stock: 4),
          _item('i1', 'Apple', categoryId: 'c1', stock: 7),
          _item('i3', 'Mystery Box', stock: 1),
        ],
        categories: const [Category(id: 'c1', name: 'Produce', sortOrder: 0)],
        purchaseOrders: [
          _po('po-1', PurchaseOrderStatus.received, [
            _line('l1', 'i1', ordered: 3, received: 3, cost: 25),
          ]),
        ],
      );

      expect(rows.map((r) => r.itemName), ['Apple', 'Mystery Box', 'Zucchini']);
      expect(rows.first.categoryName, 'Produce');
      expect(rows.first.availableStock, 7);
      expect(rows.first.averageCost, 25);

      final mystery = rows[1];
      expect(mystery.categoryName, 'Uncategorized');
      expect(mystery.averageCost, isNull);
      expect(mystery.costSampleCount, 0);
    });

    test('falls back to Uncategorized for an unknown category id', () {
      final rows = buildItemStockCostRows(
        items: [_item('i1', 'Orphan', categoryId: 'gone')],
        categories: const [],
        purchaseOrders: const [],
      );

      expect(rows.single.categoryName, 'Uncategorized');
    });
  });
}
