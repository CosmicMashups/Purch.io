import 'department_sales_models.dart';
import 'inventory_report_models.dart';
import 'sales_dashboard_models.dart';
import 'staff_performance_models.dart';

/// F1/F3/F4 — read-only report queries. F2 (BIR X/Z-reading, shift
/// reconciliation) already has its own repository from Phase 4
/// (BirReadingRepository/ShiftRepository in the pos feature) and isn't
/// duplicated here.
abstract class ReportsRepository {
  Future<SalesDashboard> getSalesDashboard({String? branchId});

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
