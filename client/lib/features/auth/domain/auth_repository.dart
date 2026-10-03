import 'auth_models.dart';

/// Contract for signing in on this device. The domain layer only knows the
/// product's sign-in model — it has no idea whether that's backed by Dio/HTTP.
///
/// Two kinds of sign-in exist:
/// * a **person** signs in with email and password ([signIn]), or unlocks a
///   paired till with their own PIN ([unlock]);
/// * a **device** is paired once with a short-lived code ([pairDevice]) and then
///   keeps its own revocable credential. It never holds a person's password.
///
/// On failure, implementations throw a `Failure` (see core/errors/failure.dart)
/// rather than returning an error code — callers catch `on Failure`.
abstract class AuthRepository {
  /// Email and password. A person in several businesses gets [ChooseBusiness]
  /// and signs in again with the chosen [tenantId]. On success the tokens are
  /// stored as a side effect.
  Future<SignInOutcome> signIn({
    required String email,
    required String password,
    String? tenantId,
  });

  /// Exchanges the one-time pairing code an admin generated for this device's
  /// own credential, stores it, and opens the first device session.
  Future<DeviceSession> pairDevice({required String pairingCode});

  /// Whether this device has been paired (holds a credential).
  Future<bool> hasDeviceCredential();

  /// Opens a session with the stored credential. A Kiosk, Order Board, Kitchen
  /// Display or Customer Display device gets its tokens stored; a Register or
  /// Warehouse device is told [DeviceSession.requiresStaff]. If the server no
  /// longer recognises the device, the stored credential is removed and an
  /// `UnauthorizedFailure` is thrown.
  Future<DeviceSession> startDeviceSession();

  /// The people who may unlock this paired Register or Warehouse device.
  Future<DeviceRoster> roster();

  /// Unlocks this device for one person with their own PIN. Stores the tokens
  /// and this device's identity. A wrong PIN throws an `UnauthorizedFailure`
  /// whose message says how many tries are left.
  Future<void> unlock({required String membershipId, required String pin});

  /// Whether an access token is stored on this device. Does NOT verify the
  /// token is still valid server-side.
  Future<bool> hasStoredSession();

  /// Ends the current session. On a paired device this is the lock: the device
  /// credential stays, so the next person simply unlocks it again.
  Future<void> logout();

  /// Forgets this device's pairing as well as its session.
  Future<void> unpair();
}
