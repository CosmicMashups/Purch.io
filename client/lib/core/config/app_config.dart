/// Build-time configuration, set via `--dart-define` at build/run time — never
/// hardcoded and never read at runtime from a server, matching the backend's
/// own "deployment mode is a config value, not a runtime toggle" design.
///
/// Cloud installs point PURCH_API_BASE_URL at the Render URL at build time —
/// one build, every device. A Local/dedicated install is different: the same
/// app build runs on every device in the store, but each physical device
/// still needs to know that specific installation's LAN address, which isn't
/// known at build time. [apiBaseUrl] resolves to a runtime override (set via
/// [setApiBaseUrlOverride], persisted by ServerConnectionStorage and restored
/// at app startup — see main()) when one has been entered on that device,
/// falling back to this build-time default otherwise.
///
/// Example:
/// `flutter run --dart-define=PURCH_API_BASE_URL=http://192.168.1.50:5000`
abstract final class AppConfig {
  static const String _defaultApiBaseUrl = String.fromEnvironment(
    'PURCH_API_BASE_URL',
    defaultValue: 'https://localhost:5001',
  );

  static String? _apiBaseUrlOverride;

  static String get apiBaseUrl => _apiBaseUrlOverride ?? _defaultApiBaseUrl;

  /// Set once at startup (from ServerConnectionStorage) and again whenever
  /// the user saves a new server address from ServerConnectionScreen. Pass
  /// null to clear back to the build-time default.
  static void setApiBaseUrlOverride(String? baseUrl) {
    _apiBaseUrlOverride = baseUrl;
  }
}
