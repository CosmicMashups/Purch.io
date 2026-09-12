import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Persists a manually-entered Local/on-prem server base URL, overriding the
/// build-time PURCH_API_BASE_URL default (see AppConfig). A Local-mode
/// installation is one backend on one store's own LAN, but every physical
/// POS device still needs to be told that server's address once — this is
/// the "manual-IP-entry" half of Phase 10's LAN-discovery/manual-IP-entry
/// requirement (no mDNS/LAN-discovery package added; that's flagged as a
/// deferred enhancement, not built here).
class ServerConnectionStorage {
  ServerConnectionStorage({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  static const _baseUrlKey = 'purch_server_base_url_override';

  final FlutterSecureStorage _storage;

  Future<void> saveBaseUrl(String baseUrl) {
    return _storage.write(key: _baseUrlKey, value: baseUrl);
  }

  Future<String?> readBaseUrl() {
    return _storage.read(key: _baseUrlKey);
  }

  Future<void> clear() {
    return _storage.delete(key: _baseUrlKey);
  }
}
