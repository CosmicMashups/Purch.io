import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Wraps the platform keychain/keystore (via flutter_secure_storage) so the
/// JWT is never held in plain SharedPreferences or an in-memory-only variable
/// that a fresh app launch would lose. This is the one place the access token
/// is read from or written to — every other layer asks this, never the
/// underlying storage plugin directly.
class SecureTokenStorage {
  SecureTokenStorage({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  static const _accessTokenKey = 'purch_access_token';
  static const _refreshTokenKey = 'purch_refresh_token';
  static const _deviceCredentialKey = 'purch_device_credential';
  static const _supervisorAttestationKey = 'purch_supervisor_attestation';

  final FlutterSecureStorage _storage;

  Future<void> saveAccessToken(String token) {
    return _storage.write(key: _accessTokenKey, value: token);
  }

  Future<String?> readAccessToken() {
    return _storage.read(key: _accessTokenKey);
  }

  Future<void> saveRefreshToken(String token) {
    return _storage.write(key: _refreshTokenKey, value: token);
  }

  Future<String?> readRefreshToken() {
    return _storage.read(key: _refreshTokenKey);
  }

  /// Persists a login/refresh response's pair together, since one is never
  /// meaningful without the other.
  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
  }) async {
    await saveAccessToken(accessToken);
    await saveRefreshToken(refreshToken);
  }

  /// The long-lived credential a paired device keeps (the one-time pairing code
  /// is exchanged for it and then discarded). It outlives sign-outs and locks:
  /// only unpairing, or an admin revoking the device, removes it.
  Future<void> saveDeviceCredential(String credential) {
    return _storage.write(key: _deviceCredentialKey, value: credential);
  }

  Future<String?> readDeviceCredential() {
    return _storage.read(key: _deviceCredentialKey);
  }

  Future<void> clearDeviceCredential() {
    return _storage.delete(key: _deviceCredentialKey);
  }

  /// The signed note the server gives a till when a manager or admin signs in on it, proving to the server later that a
  /// supervisor was working this till. Attached to offline discount sales. It belongs to that one sign-in, so it is replaced
  /// (or removed) by whoever signs in next and removed on sign-out.
  Future<void> saveSupervisorAttestation(String attestation) {
    return _storage.write(key: _supervisorAttestationKey, value: attestation);
  }

  Future<String?> readSupervisorAttestation() {
    return _storage.read(key: _supervisorAttestationKey);
  }

  Future<void> clearSupervisorAttestation() {
    return _storage.delete(key: _supervisorAttestationKey);
  }

  Future<void> clear() async {
    await _storage.delete(key: _accessTokenKey);
    await _storage.delete(key: _refreshTokenKey);
    await _storage.delete(key: _supervisorAttestationKey);
  }
}
