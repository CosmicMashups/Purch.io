import 'package:dio/dio.dart';

import '../../../core/auth/jwt_claims.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../../../core/storage/secure_token_storage.dart';
import '../../pos/domain/transaction_models.dart';
import '../domain/kitchen_display_repository.dart';

class KitchenDisplayRepositoryImpl implements KitchenDisplayRepository {
  KitchenDisplayRepositoryImpl({
    required ApiClient apiClient,
    required SecureTokenStorage tokenStorage,
  }) : _apiClient = apiClient,
       _tokenStorage = tokenStorage;

  final ApiClient _apiClient;
  final SecureTokenStorage _tokenStorage;

  @override
  Future<String?> currentBranchId() async {
    final token = await _tokenStorage.readAccessToken();
    if (token == null) return null;
    return deviceClaimsFromJwt(token)?.branchId;
  }

  @override
  Future<List<Transaction>> listPendingOrders(String branchId) async {
    try {
      final response = await _apiClient.dio.get<List<dynamic>>(
        '/kitchen-display/pending',
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
  Future<Transaction> updateStatus(String transactionId, KitchenStatus status) async {
    try {
      final response = await _apiClient.dio.put<Map<String, dynamic>>(
        '/kitchen-display/orders/$transactionId/status',
        data: UpdateKitchenStatusRequest(kitchenStatus: status).toJson(),
      );
      return Transaction.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
