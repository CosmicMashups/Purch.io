import '../../catalog/domain/category_models.dart';
import '../../catalog/domain/item_models.dart';
import 'sales_dashboard_models.dart';

/// Revenue grouped by catalog category — the category-level sibling of
/// [DepartmentSalesSummary].
///
/// Unlike department sales, the backend has no `/reports/category-sales`
/// endpoint, and categories are a catalog concern rather than a reporting
/// one. Rather than duplicate catalog HTTP calls inside the reports
/// repository, this is derived client-side by joining the sales dashboard's
/// per-item revenue against the catalog's item→category mapping. The join is
/// a pure function so it stays testable and can be replaced wholesale if a
/// real endpoint lands.
class CategorySalesSummary {
  const CategorySalesSummary({
    required this.categoryId,
    required this.categoryName,
    required this.revenue,
    required this.quantitySold,
  });

  /// Null means "Uncategorized" — items with no category assigned.
  final String? categoryId;
  final String categoryName;
  final double revenue;
  final double quantitySold;
}

/// Joins top-selling item revenue onto categories.
///
/// Caveat worth knowing when reading the chart: the dashboard returns only
/// the *top* selling items, so this is a top-items-by-category breakdown, not
/// an exhaustive one. The UI labels it accordingly.
List<CategorySalesSummary> aggregateCategorySales({
  required List<TopSellingItem> topSellingItems,
  required List<Item> items,
  required List<Category> categories,
}) {
  final categoryIdByItemId = <String, String?>{
    for (final item in items) item.id: item.categoryId,
  };
  final categoryNameById = <String, String>{
    for (final category in categories) category.id: category.name,
  };

  final revenueByCategory = <String?, double>{};
  final quantityByCategory = <String?, double>{};

  for (final sold in topSellingItems) {
    final categoryId = categoryIdByItemId[sold.itemId];
    revenueByCategory[categoryId] =
        (revenueByCategory[categoryId] ?? 0) + sold.revenue;
    quantityByCategory[categoryId] =
        (quantityByCategory[categoryId] ?? 0) + sold.quantitySold;
  }

  final summaries = [
    for (final entry in revenueByCategory.entries)
      CategorySalesSummary(
        categoryId: entry.key,
        categoryName:
            entry.key == null
                ? 'Uncategorized'
                : categoryNameById[entry.key] ?? 'Uncategorized',
        revenue: entry.value,
        quantitySold: quantityByCategory[entry.key] ?? 0,
      ),
  ];

  summaries.sort((a, b) => b.revenue.compareTo(a.revenue));
  return summaries;
}
