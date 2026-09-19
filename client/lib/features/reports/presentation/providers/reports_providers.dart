import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../data/reports_repository_impl.dart';
import '../../domain/category_sales_models.dart';
import '../../domain/department_sales_models.dart';
import '../../domain/inventory_report_models.dart';
import '../../../../core/data/data_refresh.dart';
import '../../domain/reports_repository.dart';
import '../../domain/sales_dashboard_models.dart';
import '../../domain/sales_trend_models.dart';
import '../../domain/staff_performance_models.dart';

part 'reports_providers.g.dart';

@Riverpod(keepAlive: true)
ReportsRepository reportsRepository(Ref ref) {
  return ReportsRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

/// Short-lived cache (see [CacheFor]): these back Home dashboard charts in a
/// plain ListView that mounts/unmounts children as they scroll, so a scroll
/// must not refetch — but the data must not outlive a minute either, and any
/// sale/stock write invalidates it explicitly via refreshStockAndSalesData.
@riverpod
Future<SalesDashboard> salesDashboard(Ref ref, {String? branchId}) {
  ref.cacheFor(const Duration(seconds: 45));
  return ref
      .watch(reportsRepositoryProvider)
      .getSalesDashboard(branchId: branchId);
}

/// Home's trend chart — one provider instance per (granularity, range)
/// combination, so switching Day → Week → Custom re-queries rather than
/// re-filtering a fixed 14-day window client-side.
@riverpod
Future<SalesTrendSeries> salesTrend(
  Ref ref, {
  String? branchId,
  required DateTime fromDate,
  required DateTime toDate,
  required SalesTrendGranularity granularity,
}) {
  ref.cacheFor(const Duration(seconds: 45));
  return ref
      .watch(reportsRepositoryProvider)
      .getSalesTrend(
        branchId: branchId,
        from: fromDate,
        to: toDate,
        granularity: granularity,
      );
}

/// Revenue by catalog category. Composed here rather than in the reports
/// repository because the item→category mapping is a catalog concern and the
/// backend exposes no category-sales endpoint — see [aggregateCategorySales].
@riverpod
Future<List<CategorySalesSummary>> categorySales(
  Ref ref, {
  String? branchId,
}) async {
  ref.cacheFor(const Duration(seconds: 45));
  final dashboard = await ref.watch(salesDashboardProvider(branchId: branchId).future);
  final items = await ref.watch(itemListProvider.future);
  final categories = await ref.watch(categoryListProvider.future);

  return aggregateCategorySales(
    topSellingItems: dashboard.topSellingItems,
    items: items,
    categories: categories,
  );
}

@riverpod
Future<MovementSummary> movementSummary(
  Ref ref, {
  String? branchId,
  // Named fromDate/toDate, not from/to — a plain "from" collides with
  // ProviderBase's own `from` (Family) member on the riverpod_generator's
  // generated provider class.
  required DateTime fromDate,
  required DateTime toDate,
}) {
  ref.cacheFor(const Duration(seconds: 45));
  return ref
      .watch(reportsRepositoryProvider)
      .getMovementSummary(branchId: branchId, from: fromDate, to: toDate);
}

@riverpod
Future<StaffPerformanceReport> staffPerformance(
  Ref ref, {
  String? branchId,
  required DateTime fromDate,
  required DateTime toDate,
}) {
  ref.cacheFor(const Duration(seconds: 45));
  return ref
      .watch(reportsRepositoryProvider)
      .getStaffPerformance(branchId: branchId, from: fromDate, to: toDate);
}

@riverpod
Future<List<DepartmentSalesSummary>> departmentSales(
  Ref ref, {
  String? branchId,
  required DateTime fromDate,
  required DateTime toDate,
}) {
  ref.cacheFor(const Duration(seconds: 45));
  return ref
      .watch(reportsRepositoryProvider)
      .getDepartmentSales(branchId: branchId, from: fromDate, to: toDate);
}

/// The low-stock CSV export isn't a plain auto-fetch value — it's an
/// explicit "generate the export" action, same shape as
/// GenerateBirReadingController in the pos feature.
@riverpod
class LowStockExportController extends _$LowStockExportController {
  @override
  FutureOr<String?> build() => null;

  Future<void> generate() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(reportsRepositoryProvider).getLowStockReorderCsv(),
    );
  }
}
