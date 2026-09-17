import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/role_nav_policy.dart';
import '../../features/auth/presentation/providers/auth_providers.dart';
import '../../features/onboarding/domain/onboarding_enums.dart';

/// Which top-level shell the router should show, derived from whether a
/// session is stored on this device and what role its token claims.
enum AuthGateState { loggedOut, kiosk, orderBoard, kitchenDisplay, staff }

/// Combines [hasStoredSessionProvider] and [storedSessionRoleProvider] into
/// the single decision the router's redirect needs — see AppRouter.
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
  final hasSession = await ref.watch(hasStoredSessionProvider.future);
  if (!hasSession) {
    return AuthGateState.loggedOut;
  }
  final role = await ref.watch(storedSessionRoleProvider.future);
  switch (role) {
    case 'Kiosk':
      return AuthGateState.kiosk;
    case 'OrderBoard':
      return AuthGateState.orderBoard;
    case 'KitchenDisplay':
      return AuthGateState.kitchenDisplay;
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
