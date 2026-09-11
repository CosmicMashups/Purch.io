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
  Future<void> pair({required String devicePairingCode}) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/kiosk/session',
        data: {'devicePairingCode': devicePairingCode},
      );

      final accessToken = response.data?['accessToken'] as String?;
      if (accessToken == null) {
        throw StateError(
          'Kiosk session response did not include an accessToken.',
        );
      }

      await _tokenStorage.saveAccessToken(accessToken);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
