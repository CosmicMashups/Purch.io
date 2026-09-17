import 'package:dio/dio.dart';

import '../../../core/auth/jwt_claims.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../../../core/storage/secure_token_storage.dart';
import '../../pos/domain/transaction_models.dart';
import '../domain/order_board_repository.dart';

class OrderBoardRepositoryImpl implements OrderBoardRepository {
  OrderBoardRepositoryImpl({
    required ApiClient apiClient,
    required SecureTokenStorage tokenStorage,
  }) : _apiClient = apiClient,
       _tokenStorage = tokenStorage;

  final ApiClient _apiClient;
  final SecureTokenStorage _tokenStorage;

  @override
  Future<void> pair({
    required String devicePairingCode,
    required String pairingPin,
  }) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/order-board/session',
        data: {
          'devicePairingCode': devicePairingCode,
          'pairingPin': pairingPin,
        },
      );

      final accessToken = response.data?['accessToken'] as String?;
      final refreshToken = response.data?['refreshToken'] as String?;
      if (accessToken == null || refreshToken == null) {
        throw StateError(
          'Order board session response did not include an accessToken/refreshToken.',
        );
      }

      await _tokenStorage.saveTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

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
        '/order-board/pending',
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
}
