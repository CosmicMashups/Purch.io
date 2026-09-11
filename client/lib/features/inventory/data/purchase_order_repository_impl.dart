import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/purchase_order_models.dart';
import '../domain/purchase_order_repository.dart';

class PurchaseOrderRepositoryImpl implements PurchaseOrderRepository {
  PurchaseOrderRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<PurchaseOrder>> listPurchaseOrders() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/purchase-orders',
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(PurchaseOrder.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<PurchaseOrder> createPurchaseOrder(
    CreatePurchaseOrderRequest request,
  ) {
    return _post('/purchase-orders', request.toJson());
  }

  @override
  Future<PurchaseOrder> markSent(String purchaseOrderId) {
    return _post('/purchase-orders/$purchaseOrderId/mark-sent', null);
  }

  @override
  Future<PurchaseOrder> cancel(String purchaseOrderId) {
    return _post('/purchase-orders/$purchaseOrderId/cancel', null);
  }

  @override
  Future<PurchaseOrder> receive(
    String purchaseOrderId,
    ReceivePurchaseOrderRequest request,
  ) {
    return _post('/purchase-orders/$purchaseOrderId/receive', request.toJson());
  }

  Future<PurchaseOrder> _post(String path, Map<String, dynamic>? data) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        path,
        data: data,
      );
      return PurchaseOrder.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
