import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/pos_repository.dart';
import '../domain/transaction_models.dart';

class PosRepositoryImpl implements PosRepository {
  PosRepositoryImpl({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<Transaction> getOrCreateOpenCart() {
    return _get('/transactions/cart', Transaction.fromJson);
  }

  @override
  Future<Transaction> addLine(AddTransactionLineRequest request) {
    return _post(
      '/transactions/cart/lines',
      request.toJson(),
      Transaction.fromJson,
    );
  }

  @override
  Future<Transaction> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  ) {
    return _put(
      '/transactions/cart/lines/$lineId',
      request.toJson(),
      Transaction.fromJson,
    );
  }

  @override
  Future<Transaction> removeLine(String lineId) {
    return _delete('/transactions/cart/lines/$lineId', Transaction.fromJson);
  }

  @override
  Future<Transaction> voidCart() {
    return _post('/transactions/cart/void', const {}, Transaction.fromJson);
  }

  @override
  Future<Transaction> recordPayment(RecordPaymentRequest request) {
    return _post(
      '/transactions/cart/payments',
      request.toJson(),
      Transaction.fromJson,
    );
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
