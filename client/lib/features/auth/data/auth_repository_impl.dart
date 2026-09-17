import 'package:dio/dio.dart';

import '../../../core/auth/jwt_claims.dart';
import '../../../core/db/daos/device_identity_dao.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../../../core/storage/secure_token_storage.dart';
import '../domain/auth_repository.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl({
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
  Future<void> login({
    required String devicePairingCode,
    required String pin,
  }) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/auth/login',
        data: {'devicePairingCode': devicePairingCode, 'pin': pin},
      );

      final accessToken = response.data?['accessToken'] as String?;
      final refreshToken = response.data?['refreshToken'] as String?;
      if (accessToken == null || refreshToken == null) {
        throw StateError('Login response did not include an accessToken/refreshToken.');
      }

      await _tokenStorage.saveTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );

      // The token is this device's only source for its own id — persist it
      // locally so it survives past this session (see DeviceIdentity's doc
      // comment for why nothing else on-device keeps it).
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

  @override
  Future<void> loginAsAdmin({
    required String email,
    required String password,
  }) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/auth/admin-login',
        data: {'email': email, 'password': password},
      );

      final accessToken = response.data?['accessToken'] as String?;
      final refreshToken = response.data?['refreshToken'] as String?;
      if (accessToken == null || refreshToken == null) {
        throw StateError('Admin login response did not include an accessToken/refreshToken.');
      }

      await _tokenStorage.saveTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );

      // An admin token carries no device/branch claims (it isn't tied to any
      // physical terminal), so there's no device identity to persist here.
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<bool> hasStoredSession() async {
    final token = await _tokenStorage.readAccessToken();
    return token != null;
  }

  @override
  Future<void> logout() async {
    // Best-effort: revoke the refresh token server-side so it can't be
    // redeemed later (e.g. if it leaked) — but a failure here must never
    // block the local logout the user is actively asking for.
    final refreshToken = await _tokenStorage.readRefreshToken();
    if (refreshToken != null) {
      try {
        await _apiClient.dio.post<void>(
          '/auth/logout',
          data: {'refreshToken': refreshToken},
        );
      } on DioException {
        // Ignored — see comment above.
      }
    }

    // Identity is left in place deliberately: it's re-derived from the same
    // device's own next login, and clearing it would erase the last-known
    // receipt number that a future offline checkout needs to resume from.
    await _tokenStorage.clear();
  }
}
