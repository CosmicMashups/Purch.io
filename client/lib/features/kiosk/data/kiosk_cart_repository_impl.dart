import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../../pos/domain/transaction_models.dart';
import '../domain/kiosk_cart_repository.dart';

class KioskCartRepositoryImpl implements KioskCartRepository {
  KioskCartRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<Transaction> getOrCreateOpenCart() {
    return _get('/kiosk/cart', Transaction.fromJson);
  }

  @override
  Future<Transaction> addLine(AddTransactionLineRequest request) {
    return _post('/kiosk/cart/lines', request.toJson(), Transaction.fromJson);
  }

  @override
  Future<Transaction> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  ) {
    return _put(
      '/kiosk/cart/lines/$lineId',
      request.toJson(),
      Transaction.fromJson,
    );
  }

  @override
  Future<Transaction> removeLine(String lineId) {
    return _delete('/kiosk/cart/lines/$lineId', Transaction.fromJson);
  }

  @override
  Future<Transaction> setOrderType(SetOrderTypeRequest request) {
    return _put(
      '/kiosk/cart/order-type',
      request.toJson(),
      Transaction.fromJson,
    );
  }

  @override
  Future<Transaction> submitOrder() {
    return _post('/kiosk/cart/submit', const {}, Transaction.fromJson);
  }

  Future<T> _get<T>(
    String path,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    try {
      final response = await _apiClient.dio.get<Map<String, dynamic>>(path);
      return fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  Future<T> _post<T>(
    String path,
    Map<String, dynamic> data,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        path,
        data: data,
      );
      return fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  Future<T> _put<T>(
    String path,
    Map<String, dynamic> data,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    try {
      final response = await _apiClient.dio.put<Map<String, dynamic>>(
        path,
        data: data,
      );
      return fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  Future<T> _delete<T>(
    String path,
    T Function(Map<String, dynamic>) fromJson,
  ) async {
    try {
      final response = await _apiClient.dio.delete<Map<String, dynamic>>(path);
      return fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
