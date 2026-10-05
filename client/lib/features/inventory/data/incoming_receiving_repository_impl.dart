import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/incoming_receiving_models.dart';
import '../domain/incoming_receiving_repository.dart';

class IncomingReceivingRepositoryImpl implements IncomingReceivingRepository {
  IncomingReceivingRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<IncomingReceiving>> listReports() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/incoming-receiving',
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(IncomingReceiving.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<IncomingReceiving> createReport(
    CreateIncomingReceivingRequest request,
  ) {
    return _post('/incoming-receiving', request.toJson());
  }

  @override
  Future<IncomingReceiving> linkPurchaseOrder(
    String reportId,
    String purchaseOrderId,
  ) {
    return _post('/incoming-receiving/$reportId/link-purchase-order', {
      'purchaseOrderId': purchaseOrderId,
    });
  }

  Future<IncomingReceiving> _post(
    String path,
    Map<String, dynamic> data,
  ) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        path,
        data: data,
      );
      return IncomingReceiving.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
