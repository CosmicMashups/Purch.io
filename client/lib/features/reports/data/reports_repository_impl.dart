import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/department_sales_models.dart';
import '../domain/inventory_report_models.dart';
import '../domain/reports_repository.dart';
import '../domain/sales_dashboard_models.dart';
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
