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
  Future<Transaction> applySeniorPwdDiscount(
    ApplySeniorPwdDiscountRequest request,
  ) {
    return _put(
      '/transactions/cart/senior-pwd-discount',
      request.toJson(),
      Transaction.fromJson,
    );
  }

  @override
  Future<Transaction> applyPromoCode(ApplyPromoCodeRequest request) {
    return _put(
      '/transactions/cart/promo-code',
      request.toJson(),
      Transaction.fromJson,
    );
  }

  @override
  Future<Transaction> setOrderType(SetOrderTypeRequest request) {
    return _put(
      '/transactions/cart/order-type',
      request.toJson(),
      Transaction.fromJson,
    );
  }

  @override
  Future<Transaction> recordPayment(RecordPaymentRequest request) {
    return _post(
      '/transactions/cart/payments',
      request.toJson(),
      Transaction.fromJson,
    );
  }

  @override
  Future<int> getLastIssuedReceiptNumber() async {
    try {
      final response = await _apiClient.dio.get<Map<String, dynamic>>(
        '/transactions/receipt-sequence',
      );
      return (response.data!['lastIssuedNumber'] as num).toInt();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<Transaction> checkout(CheckoutRequest request) {
    return _post(
      '/transactions/checkout',
      request.toJson(),
      Transaction.fromJson,
    );
  }

  @override
  Future<List<Transaction>> listPendingKioskOrders(String branchId) async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/transactions/kiosk-pending',
        queryParameters: {'branchId': branchId},
      );
      return (response.data ?? [])
          .cast<Map<String, dynamic>>()
          .map(Transaction.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<Transaction> claimKioskOrder(String transactionId) {
    return _post(
      '/transactions/kiosk-pending/$transactionId/claim',
      const {},
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
