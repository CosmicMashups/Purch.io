import 'department_sales_models.dart';
import 'inventory_report_models.dart';
import 'sales_dashboard_models.dart';
import 'sales_trend_models.dart';
import 'staff_performance_models.dart';

/// F1/F3/F4 — read-only report queries. F2 (BIR X/Z-reading, shift
/// reconciliation) already has its own repository from Phase 4
/// (BirReadingRepository/ShiftRepository in the pos feature) and isn't
/// duplicated here.
abstract class ReportsRepository {
  Future<SalesDashboard> getSalesDashboard({String? branchId});

  /// Revenue over an arbitrary window, bucketed at [granularity] — Home's
  /// Day/Week/Month/Year/Custom trend control. Also returns the comparable
  /// preceding window so the caller can show a real period-over-period delta.
  Future<SalesTrendSeries> getSalesTrend({
    String? branchId,
    required DateTime from,
    required DateTime to,
    required SalesTrendGranularity granularity,
  });

  Future<MovementSummary> getMovementSummary({
    String? branchId,
    required DateTime from,
    required DateTime to,
  });

  Future<String> getLowStockReorderCsv();

  Future<StaffPerformanceReport> getStaffPerformance({
    String? branchId,
    required DateTime from,
    required DateTime to,
  });

  /// B6's split sales-attribution report.
  Future<List<DepartmentSalesSummary>> getDepartmentSales({
    String? branchId,
    required DateTime from,
    required DateTime to,
  });
}
