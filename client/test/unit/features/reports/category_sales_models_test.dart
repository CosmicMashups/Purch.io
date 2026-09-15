import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/reports/domain/category_sales_models.dart';
import 'package:purch_client/features/reports/domain/sales_dashboard_models.dart';

Item _item(String id, String? categoryId) => Item(
  id: id,
  name: 'Item $id',
  sku: null,
  barcode: null,
  categoryId: categoryId,
  basePrice: 10,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: const [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

TopSellingItem _sold(String itemId, double revenue, double quantity) =>
    TopSellingItem(
      itemId: itemId,
      itemName: 'Item $itemId',
      quantitySold: quantity,
      revenue: revenue,
    );

void main() {
  test('sums item revenue into its category, highest first', () {
    final summaries = aggregateCategorySales(
      topSellingItems: [_sold('i1', 100, 2), _sold('i2', 50, 1), _sold('i3', 400, 4)],
      items: [_item('i1', 'c1'), _item('i2', 'c1'), _item('i3', 'c2')],
      categories: const [
        Category(id: 'c1', name: 'Drinks', sortOrder: 0),
        Category(id: 'c2', name: 'Meals', sortOrder: 1),
      ],
    );

    expect(summaries.map((s) => s.categoryName), ['Meals', 'Drinks']);
    expect(summaries.first.revenue, 400);
    expect(summaries.last.revenue, 150);
    expect(summaries.last.quantitySold, 3);
  });

  test('groups items with no category under Uncategorized', () {
    final summaries = aggregateCategorySales(
      topSellingItems: [_sold('i1', 100, 1)],
      items: [_item('i1', null)],
      categories: const [],
    );

    expect(summaries.single.categoryId, isNull);
    expect(summaries.single.categoryName, 'Uncategorized');
  });

  test('treats an item missing from the catalog as uncategorized', () {
    final summaries = aggregateCategorySales(
      topSellingItems: [_sold('ghost', 75, 1)],
      items: const [],
      categories: const [],
    );

    expect(summaries.single.categoryName, 'Uncategorized');
    expect(summaries.single.revenue, 75);
  });

  test('is empty when nothing sold', () {
    expect(
      aggregateCategorySales(
        topSellingItems: const [],
        items: [_item('i1', 'c1')],
        categories: const [Category(id: 'c1', name: 'Drinks', sortOrder: 0)],
      ),
      isEmpty,
    );
  });
}
