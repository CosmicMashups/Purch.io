import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/inventory_movement_models.dart';
import '../domain/inventory_repository.dart';

class InventoryRepositoryImpl implements InventoryRepository {
  InventoryRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<InventoryDashboard> getDashboard() async {
    try {
      final response = await _apiClient.dio.get<Map<String, dynamic>>(
        '/inventory/dashboard',
      );
      return InventoryDashboard.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<List<InventoryMovement>> listMovements({
    String? itemId,
    String? branchId,
    MovementType? type,
  }) async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/inventory/movements',
        queryParameters: {
          if (itemId != null) 'itemId': itemId,
          if (branchId != null) 'branchId': branchId,
          if (type != null) 'type': type.index,
        },
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(InventoryMovement.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<InventoryMovement> recordMovement(
    RecordMovementRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/inventory/movements',
        data: request.toJson(),
      );
      return InventoryMovement.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
