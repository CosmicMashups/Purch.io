import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/department_sales_models.dart';
import '../domain/inventory_report_models.dart';
import '../domain/reports_repository.dart';
import '../domain/sales_dashboard_models.dart';
import '../domain/sales_trend_models.dart';
import '../domain/staff_performance_models.dart';

class ReportsRepositoryImpl implements ReportsRepository {
  ReportsRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<SalesDashboard> getSalesDashboard({String? branchId}) async {
    try {
      final response = await _apiClient.dio.get<Map<String, dynamic>>(
        '/reports/sales-dashboard',
        queryParameters: {if (branchId != null) 'branchId': branchId},
      );
      return SalesDashboard.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  /// There is no `/reports/sales-trend` endpoint yet (see
  /// `backend/src/Purch.Api/Endpoints/ReportingEndpoints.cs` — the reporting
  /// surface is sales-dashboard / movement-summary / low-stock-export /
  /// staff-performance / department-sales). The sales dashboard already
  /// returns a daily revenue series, and every coarser bucket is a fold over
  /// it, so the trend is aggregated here from that one call.
  ///
  /// Consequence to be aware of: the trend can only reach as far back as the
  /// dashboard's own daily window. [SalesTrendSeries] reports the window it
  /// actually covered, and Home says so in the chart subtitle rather than
  /// drawing a flat line over months the server never sent. Swapping this for
  /// a real endpoint is a change to this method only.
  @override
  Future<SalesTrendSeries> getSalesTrend({
    String? branchId,
    required DateTime from,
    required DateTime to,
    required SalesTrendGranularity granularity,
  }) async {
    final dashboard = await getSalesDashboard(branchId: branchId);
    return SalesTrendSeries.fromDailyPoints(
      dashboard.trend,
      granularity: granularity,
      from: from,
      to: to,
    );
  }

  @override
  Future<MovementSummary> getMovementSummary({
    String? branchId,
    required DateTime from,
    required DateTime to,
  }) async {
    try {
      final response = await _apiClient.dio.get<Map<String, dynamic>>(
        '/reports/inventory/movement-summary',
        queryParameters: {
          if (branchId != null) 'branchId': branchId,
          'from': from.toUtc().toIso8601String(),
          'to': to.toUtc().toIso8601String(),
        },
      );
      return MovementSummary.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<String> getLowStockReorderCsv() async {
    try {
      final response = await _apiClient.dio.get<String>(
        '/reports/inventory/low-stock-export.csv',
        options: Options(responseType: ResponseType.plain),
      );
      return response.data ?? '';
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<StaffPerformanceReport> getStaffPerformance({
    String? branchId,
    required DateTime from,
    required DateTime to,
  }) async {
    try {
      final response = await _apiClient.dio.get<Map<String, dynamic>>(
        '/reports/staff-performance',
        queryParameters: {
          if (branchId != null) 'branchId': branchId,
          'from': from.toUtc().toIso8601String(),
          'to': to.toUtc().toIso8601String(),
        },
      );
      return StaffPerformanceReport.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<List<DepartmentSalesSummary>> getDepartmentSales({
    String? branchId,
    required DateTime from,
    required DateTime to,
  }) async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/reports/department-sales',
        queryParameters: {
          if (branchId != null) 'branchId': branchId,
          'from': from.toUtc().toIso8601String(),
          'to': to.toUtc().toIso8601String(),
        },
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(DepartmentSalesSummary.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
