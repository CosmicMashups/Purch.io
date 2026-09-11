import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/credit_ledger_models.dart';
import '../domain/credit_ledger_repository.dart';

class CreditLedgerRepositoryImpl implements CreditLedgerRepository {
  CreditLedgerRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<CustomerCreditLedger>> listLedgers() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/credit-ledger',
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(CustomerCreditLedger.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<CustomerCreditLedger> createLedger(
    CreateCustomerCreditLedgerRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/credit-ledger',
        data: request.toJson(),
      );
      return CustomerCreditLedger.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<CustomerCreditLedger> recordPayment(
    String ledgerId,
    RecordCreditPaymentRequest request,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/credit-ledger/$ledgerId/payments',
        data: request.toJson(),
      );
      return CustomerCreditLedger.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<List<CreditReminder>> listReminders({int withinDays = 7}) async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/credit-ledger/reminders',
        queryParameters: {'withinDays': withinDays},
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(CreditReminder.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
