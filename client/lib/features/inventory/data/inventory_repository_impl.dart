import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/inventory_item_models.dart';
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
    DateTime? before,
    String? beforeId,
    int? limit,
  }) async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/inventory/movements',
        queryParameters: {
          if (itemId != null) 'itemId': itemId,
          if (branchId != null) 'branchId': branchId,
          if (type != null) 'type': type.index,
          if (before != null) 'before': before.toUtc().toIso8601String(),
          if (beforeId != null) 'beforeId': beforeId,
          if (limit != null) 'limit': limit,
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

  @override
  Future<List<InventoryItem>> listInventoryItems() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/inventory-items',
      );
      // Giving a Cashier item a recipe retires its own auto-paired stock record (inactive, kept only for
      // its movement history). It has no place in the list or as a recipe ingredient.
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(InventoryItem.fromJson)
          .where((item) => item.isActive)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<InventoryItem> createInventoryItem(
    CreateInventoryItemRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/inventory-items',
        data: request.toJson(),
      );
      return InventoryItem.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<InventoryItem> updateInventoryItem(
    String id,
    UpdateInventoryItemRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.put<Map<String, dynamic>>(
        '/inventory-items/$id',
        data: request.toJson(),
      );
      return InventoryItem.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<InventoryItem> updatePhysicalCount(
    String id,
    UpdatePhysicalCountRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/inventory-items/$id/physical-count',
        data: request.toJson(),
      );
      return InventoryItem.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<InventoryItem> receiveInventoryStock(
    String id,
    ReceiveInventoryStockRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/inventory-items/$id/receive',
        data: request.toJson(),
      );
      return InventoryItem.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<List<ItemRecipeLine>> getItemRecipe(String itemId) async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/items/$itemId/recipe',
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(ItemRecipeLine.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<List<ItemRecipeLine>> replaceItemRecipe(
    String itemId,
    ReplaceItemRecipeRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.put<List<dynamic>>(
        '/items/$itemId/recipe',
        data: request.toJson(),
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(ItemRecipeLine.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
