/// Build-time configuration, set via `--dart-define` at build/run time — never
/// hardcoded and never read at runtime from a server, matching the backend's
/// own "deployment mode is a config value, not a runtime toggle" design.
///
/// Cloud installs point PURCH_API_BASE_URL at the Render URL; Local/dedicated
/// installs point it at the on-prem server's LAN IP or hostname instead. The
/// app itself never needs to know or care which one it's talking to — only
/// the URL differs.
///
/// Example:
/// `flutter run --dart-define=PURCH_API_BASE_URL=http://192.168.1.50:5000`
abstract final class AppConfig {
  static const String apiBaseUrl = String.fromEnvironment(
    'PURCH_API_BASE_URL',
    defaultValue: 'https://localhost:5001',
  );
}
