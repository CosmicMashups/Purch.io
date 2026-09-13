import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/role_nav_policy.dart';
import '../../features/auth/presentation/providers/auth_providers.dart';
import '../../features/onboarding/domain/onboarding_enums.dart';

/// Which top-level shell the router should show, derived from whether a
/// session is stored on this device and what role its token claims.
enum AuthGateState { loggedOut, kiosk, staff }

/// Combines [hasStoredSessionProvider] and [storedSessionRoleProvider] into
/// the single decision the router's redirect needs — see AppRouter.
final authGateProvider = FutureProvider<AuthGateState>((ref) async {
  final hasSession = await ref.watch(hasStoredSessionProvider.future);
  if (!hasSession) {
    return AuthGateState.loggedOut;
  }
  final role = await ref.watch(storedSessionRoleProvider.future);
  return role == 'Kiosk' ? AuthGateState.kiosk : AuthGateState.staff;
});

/// The signed-in staff member's [StaffRole], parsed from the JWT role claim.
/// `null` while unresolved/unparseable — callers should treat that as the
/// least-privileged case (see [tabsForRole]).
final currentStaffRoleProvider = FutureProvider<StaffRole?>((ref) async {
  final claim = await ref.watch(storedSessionRoleProvider.future);
  return staffRoleFromClaim(claim);
});
