import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../../../core/storage/secure_token_storage.dart';
import '../domain/kiosk_session_repository.dart';

class KioskSessionRepositoryImpl implements KioskSessionRepository {
  KioskSessionRepositoryImpl({
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
        '/kiosk/session',
        data: {
          'devicePairingCode': devicePairingCode,
          'pairingPin': pairingPin,
        },
      );

      final accessToken = response.data?['accessToken'] as String?;
      final refreshToken = response.data?['refreshToken'] as String?;
      if (accessToken == null || refreshToken == null) {
        throw StateError(
          'Kiosk session response did not include an accessToken/refreshToken.',
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
}
