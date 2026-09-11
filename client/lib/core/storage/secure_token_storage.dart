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

  final FlutterSecureStorage _storage;

  Future<void> saveAccessToken(String token) {
    return _storage.write(key: _accessTokenKey, value: token);
  }

  Future<String?> readAccessToken() {
    return _storage.read(key: _accessTokenKey);
  }

  Future<void> clear() {
    return _storage.delete(key: _accessTokenKey);
  }
}
