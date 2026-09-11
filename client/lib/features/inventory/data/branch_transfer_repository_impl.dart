import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/branch_transfer_models.dart';
import '../domain/branch_transfer_repository.dart';

class BranchTransferRepositoryImpl implements BranchTransferRepository {
  BranchTransferRepositoryImpl({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<List<BranchTransfer>> listTransfers() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/branch-transfers',
      );
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(BranchTransfer.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<BranchTransfer> createTransfer(
    CreateBranchTransferRequest request,
  ) async {
    return _post('/branch-transfers', request.toJson());
  }

  @override
  Future<BranchTransfer> markInTransit(String branchTransferId) {
    return _post('/branch-transfers/$branchTransferId/mark-in-transit', null);
  }

  @override
  Future<BranchTransfer> markReceived(String branchTransferId) {
    return _post('/branch-transfers/$branchTransferId/mark-received', null);
  }

  Future<BranchTransfer> _post(String path, Map<String, dynamic>? data) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        path,
        data: data,
      );
      return BranchTransfer.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
