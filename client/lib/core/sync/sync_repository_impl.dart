import 'package:dio/dio.dart';

import '../network/api_client.dart';
import '../network/failure_mapper.dart';
import 'sync_dto.dart';
import 'sync_repository.dart';

class SyncRepositoryImpl implements SyncRepository {
  SyncRepositoryImpl({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<SyncBatchResult> syncBatch(List<SyncItemRequest> items) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/sync',
        data: {'items': items.map((item) => item.toJson()).toList()},
      );
      return SyncBatchResult.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<List<FlaggedSyncRecord>> listFlagged() async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>('/sync/flagged');
      return response.data!
          .cast<Map<String, dynamic>>()
          .map(FlaggedSyncRecord.fromJson)
          .toList();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<FlaggedSyncRecord> acknowledgeFlagged(String syncedRecordId) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/sync/flagged/$syncedRecordId/acknowledge',
      );
      return FlaggedSyncRecord.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
