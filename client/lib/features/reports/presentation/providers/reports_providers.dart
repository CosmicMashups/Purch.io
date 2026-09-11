import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/reports_repository_impl.dart';
import '../../domain/department_sales_models.dart';
import '../../domain/inventory_report_models.dart';
import '../../domain/reports_repository.dart';
import '../../domain/sales_dashboard_models.dart';
import '../../domain/staff_performance_models.dart';

part 'reports_providers.g.dart';

@Riverpod(keepAlive: true)
ReportsRepository reportsRepository(Ref ref) {
  return ReportsRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

@riverpod
Future<SalesDashboard> salesDashboard(Ref ref, {String? branchId}) {
  return ref
      .watch(reportsRepositoryProvider)
      .getSalesDashboard(branchId: branchId);
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
