import 'package:purch_client/features/reports/domain/inventory_report_models.dart';
import 'package:purch_client/features/reports/domain/reports_repository.dart';
import 'package:purch_client/features/reports/domain/sales_dashboard_models.dart';
import 'package:purch_client/features/reports/domain/staff_performance_models.dart';

class FakeReportsRepository implements ReportsRepository {
  FakeReportsRepository({
    SalesDashboard? salesDashboard,
    MovementSummary? movementSummary,
    String? lowStockCsv,
    StaffPerformanceReport? staffPerformance,
  }) : _salesDashboard =
           salesDashboard ??
           const SalesDashboard(
             revenueToday: 0,
             revenueLast7Days: 0,
             revenueLast30Days: 0,
             trend: [],
             topSellingItems: [],
             branchComparison: [],
           ),
       _movementSummary = movementSummary,
       _lowStockCsv = lowStockCsv ?? '',
       _staffPerformance =
           staffPerformance ??
           const StaffPerformanceReport(sales: [], shiftAttendance: []);

  final SalesDashboard _salesDashboard;
  final MovementSummary? _movementSummary;
  final String _lowStockCsv;
  final StaffPerformanceReport _staffPerformance;

  @override
  Future<SalesDashboard> getSalesDashboard({String? branchId}) async =>
      _salesDashboard;

  @override
  Future<MovementSummary> getMovementSummary({
    String? branchId,
    required DateTime from,
    required DateTime to,
  }) async =>
      _movementSummary ?? MovementSummary(from: from, to: to, byType: const []);

  @override
  Future<String> getLowStockReorderCsv() async => _lowStockCsv;

  @override
  Future<StaffPerformanceReport> getStaffPerformance({
    String? branchId,
    required DateTime from,
    required DateTime to,
  }) async => _staffPerformance;
}
