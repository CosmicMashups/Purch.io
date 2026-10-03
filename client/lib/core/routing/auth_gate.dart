import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/jwt_claims.dart';
import '../auth/role_nav_policy.dart';
import '../errors/failure.dart';
import '../../features/auth/presentation/providers/auth_providers.dart';
import '../../features/onboarding/domain/onboarding_enums.dart';

/// Which top-level shell the router should show, derived from whether a
/// session is stored on this device, whether the device is paired, and what
/// role its token claims.
///
/// * [loggedOut]: no session and not paired — the email sign-in (or pairing).
/// * [locked]: a paired Register or Warehouse device with nobody unlocked — the
///   lock screen where a person picks their name and types their PIN.
/// * [unsupportedDevice]: a paired device type this app does not run (the
///   Customer Display runs in the web app).
enum AuthGateState {
  loggedOut,
  locked,
  kiosk,
  orderBoard,
  kitchenDisplay,
  unsupportedDevice,
  staff,
}

/// Combines the stored session, the device credential and the token's role
/// into the single decision the router's redirect needs — see AppRouter.
///
/// The stored-session check itself usually resolves within a millisecond
/// (a local secure-storage read), which let the router's very first redirect
/// jump straight past `/splash` before a single frame of it had painted —
/// on a real device this showed as the native pre-Flutter window background
/// (fixed separately for Android in styles.xml) with no splash in between.
/// The floor below guarantees the splash is actually on screen for a moment,
/// matching its own fade-in animation duration.
const _minimumSplashDuration = Duration(milliseconds: 500);

final authGateProvider = FutureProvider<AuthGateState>((ref) async {
  final results = await Future.wait([
    _resolveGateState(ref),
    Future<void>.delayed(_minimumSplashDuration),
  ]);
  return results[0] as AuthGateState;
});

Future<AuthGateState> _resolveGateState(Ref ref) async {
  final repository = ref.read(authRepositoryProvider);
  final hasSession = await ref.watch(hasStoredSessionProvider.future);

  if (!hasSession) {
    if (!await repository.hasDeviceCredential()) {
      return AuthGateState.loggedOut;
    }

    // Paired, but no access token (a fresh start, or the session lapsed). A
    // Kiosk, Order Board or Kitchen Display gets its tokens straight back; a
    // Register or Warehouse device waits for a person to unlock it.
    try {
      final session = await repository.startDeviceSession();
      if (session.requiresStaff) {
        return AuthGateState.locked;
      }
    } on Failure {
      // The server was unreachable, or no longer knows this device (the
      // repository dropped the credential). Unreachable: let the person try
      // the lock screen, which can retry. Forgotten: pair again.
      return await repository.hasDeviceCredential()
          ? AuthGateState.locked
          : AuthGateState.loggedOut;
    }
  }

  // Read straight from storage: starting the device session above may have just
  // written the token, after any cached provider last looked.
  final token = await ref.read(secureTokenStorageProvider).readAccessToken();
  final role = token == null ? null : roleClaimFromJwt(token);
  switch (role) {
    case 'Kiosk':
      return AuthGateState.kiosk;
    case 'OrderBoard':
      return AuthGateState.orderBoard;
    case 'KitchenDisplay':
      return AuthGateState.kitchenDisplay;
    case 'CustomerDisplay':
      return AuthGateState.unsupportedDevice;
    default:
      return AuthGateState.staff;
  }
}

/// The signed-in staff member's [StaffRole], parsed from the JWT role claim.
/// `null` while unresolved/unparseable — callers should treat that as the
/// least-privileged case (see [tabsForRole]).
final currentStaffRoleProvider = FutureProvider<StaffRole?>((ref) async {
  final claim = await ref.watch(storedSessionRoleProvider.future);
  return staffRoleFromClaim(claim);
});
