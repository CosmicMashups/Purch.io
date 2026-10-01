import 'package:dio/dio.dart';

import '../../../core/auth/jwt_claims.dart';
import '../../../core/db/daos/device_identity_dao.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../../../core/storage/secure_token_storage.dart';
import '../domain/kiosk_session_repository.dart';

class KioskSessionRepositoryImpl implements KioskSessionRepository {
  KioskSessionRepositoryImpl({
    required ApiClient apiClient,
    required SecureTokenStorage tokenStorage,
    required DeviceIdentityDao deviceIdentityDao,
  }) : _apiClient = apiClient,
       _tokenStorage = tokenStorage,
       _deviceIdentityDao = deviceIdentityDao;

  final ApiClient _apiClient;
  final SecureTokenStorage _tokenStorage;
  final DeviceIdentityDao _deviceIdentityDao;

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

      // This kiosk's own cart lives entirely on-device (see
      // LocalFirstKioskCartRepository), which needs this terminal's identity
      // to scope its draft — the token is the only place that identity comes from.
      final claims = deviceClaimsFromJwt(accessToken);
      if (claims != null) {
        await _deviceIdentityDao.saveIdentity(
          deviceId: claims.deviceId,
          tenantId: claims.tenantId,
          branchId: claims.branchId,
        );
      }
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
