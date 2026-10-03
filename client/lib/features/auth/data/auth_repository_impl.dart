import 'package:dio/dio.dart';

import '../../../core/auth/jwt_claims.dart';
import '../../../core/db/daos/device_identity_dao.dart';
import '../../../core/errors/failure.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../../../core/storage/secure_token_storage.dart';
import '../domain/auth_models.dart';
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
  Future<SignInOutcome> signIn({
    required String email,
    required String password,
    String? tenantId,
  }) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/auth/sign-in',
        data: {'email': email, 'password': password, 'tenantId': tenantId},
      );
      final body = response.data ?? const <String, dynamic>{};

      if (body['chooseBusiness'] == true) {
        final businesses =
            (body['businesses'] as List<dynamic>? ?? const [])
                .map((b) => BusinessChoice.fromJson(b as Map<String, dynamic>))
                .toList();
        return ChooseBusiness(businesses);
      }

      await _storeTokens(body, 'Sign-in');
      // A person's own session is not tied to a physical terminal, so there is
      // no device identity to keep here.
      return const SignedIn();
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<DeviceSession> pairDevice({required String pairingCode}) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/devices/pair',
        data: {'pairingCode': pairingCode},
      );
      final credential = response.data?['deviceCredential'] as String?;
      if (credential == null) {
        throw StateError('Pairing response did not include a deviceCredential.');
      }
      await _tokenStorage.saveDeviceCredential(credential);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
    return startDeviceSession();
  }

  @override
  Future<bool> hasDeviceCredential() async {
    return (await _tokenStorage.readDeviceCredential()) != null;
  }

  @override
  Future<DeviceSession> startDeviceSession() async {
    final credential = await _requireCredential();

    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/devices/session',
        data: {'deviceCredential': credential},
      );
      final body = response.data ?? const <String, dynamic>{};
      final deviceType =
          PairedDeviceType.values[(body['deviceType'] as int?) ?? 0];
      final requiresStaff = body['requiresStaff'] == true;

      if (!requiresStaff) {
        await _storeTokens(body, 'Device session');
        await _saveIdentityFrom(body['accessToken'] as String);
      }
      return DeviceSession(
        requiresStaff: requiresStaff,
        deviceType: deviceType,
        name: body['name'] as String?,
      );
    } on DioException catch (exception) {
      final failure = mapDioExceptionToFailure(exception);
      // The server answered and does not know this device (revoked or paired
      // again elsewhere): forget the credential so the app offers to pair.
      if (failure is UnauthorizedFailure) {
        await _tokenStorage.clearDeviceCredential();
      }
      throw failure;
    }
  }

  @override
  Future<DeviceRoster> roster() async {
    final credential = await _requireCredential();
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/devices/roster',
        data: {'deviceCredential': credential},
      );
      final body = response.data ?? const <String, dynamic>{};
      final people =
          (body['people'] as List<dynamic>? ?? const [])
              .map((p) => RosterPerson.fromJson(p as Map<String, dynamic>))
              .toList();
      return DeviceRoster(
        deviceName: body['deviceName'] as String?,
        people: people,
      );
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  @override
  Future<void> unlock({
    required String membershipId,
    required String pin,
  }) async {
    final credential = await _requireCredential();
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/devices/unlock',
        data: {
          'deviceCredential': credential,
          'membershipId': membershipId,
          'pin': pin,
        },
      );
      final body = response.data ?? const <String, dynamic>{};
      await _storeTokens(body, 'Unlock');
      // The token is this device's only source for its own id — persist it
      // locally so it survives past this session (see DeviceIdentity's doc
      // comment for why nothing else on-device keeps it).
      await _saveIdentityFrom(body['accessToken'] as String);
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
    // block the local lock/sign-out the user is actively asking for.
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
    // device's own next unlock, and clearing it would erase the last-known
    // receipt number that a future offline checkout needs to resume from.
    await _tokenStorage.clear();
  }

  @override
  Future<void> unpair() async {
    await logout();
    await _tokenStorage.clearDeviceCredential();
  }

  Future<String> _requireCredential() async {
    final credential = await _tokenStorage.readDeviceCredential();
    if (credential == null) {
      throw const UnauthorizedFailure('This device is not paired yet.');
    }
    return credential;
  }

  Future<void> _storeTokens(Map<String, dynamic> body, String what) async {
    final accessToken = body['accessToken'] as String?;
    final refreshToken = body['refreshToken'] as String?;
    if (accessToken == null || refreshToken == null) {
      throw StateError(
        '$what response did not include an accessToken/refreshToken.',
      );
    }
    await _tokenStorage.saveTokens(
      accessToken: accessToken,
      refreshToken: refreshToken,
    );
  }

  Future<void> _saveIdentityFrom(String accessToken) async {
    final claims = deviceClaimsFromJwt(accessToken);
    if (claims != null) {
      await _deviceIdentityDao.saveIdentity(
        deviceId: claims.deviceId,
        tenantId: claims.tenantId,
        branchId: claims.branchId,
      );
    }
  }
}
