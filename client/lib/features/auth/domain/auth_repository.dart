/// Contract for authenticating a staff member on this device. The domain
/// layer only knows "you can log in and check who's logged in" — it has no
/// idea whether that's backed by Dio/HTTP or anything else, matching the
/// same interface-first pattern used throughout the backend.
///
/// On failure, implementations throw a `Failure` (see core/errors/failure.dart)
/// rather than returning an error code — callers catch `on Failure`.
abstract class AuthRepository {
  /// Logs in with this device's pairing code and a staff PIN. On success, the
  /// access token is persisted (via SecureTokenStorage) as a side effect —
  /// callers don't need to handle the token themselves.
  Future<void> login({required String devicePairingCode, required String pin});

  /// Whether a previously-issued access token is still stored on this device.
  /// Used at app startup to decide whether to show the login screen or skip
  /// straight to the app shell — this does NOT verify the token is still
  /// valid server-side, only that one exists locally.
  Future<bool> hasStoredSession();

  Future<void> logout();
}
