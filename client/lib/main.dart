import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'features/auth/presentation/providers/auth_providers.dart';
import 'features/auth/presentation/screens/login_screen.dart';
import 'features/onboarding/presentation/screens/audit_log_screen.dart';
import 'features/onboarding/presentation/screens/branch_list_screen.dart';
import 'features/onboarding/presentation/screens/device_list_screen.dart';
import 'features/onboarding/presentation/screens/staff_list_screen.dart';
import 'features/onboarding/presentation/screens/tenant_settings_screen.dart';

void main() {
  runApp(const ProviderScope(child: PurchApp()));
}

class PurchApp extends StatelessWidget {
  const PurchApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Purch.io',
      // Placeholder seed color — real per-tenant branding (logo, theme color,
      // font) is built at runtime from CachedBranding once onboarding (Phase 2)
      // lands. See core/theming/theme_builder.dart (not yet built).
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.indigo),
      ),
      home: const _StartupGate(),
    );
  }
}

/// Decides, once at launch, whether this device already has a stored session
/// (skip straight to the app shell) or needs to show the login screen —
/// this does NOT verify the token is still valid server-side, only that one
/// is present locally (see AuthRepository.hasStoredSession).
class _StartupGate extends ConsumerWidget {
  const _StartupGate();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final hasSession = ref.watch(hasStoredSessionProvider);

    return hasSession.when(
      loading:
          () =>
              const Scaffold(body: Center(child: CircularProgressIndicator())),
      error:
          (error, stackTrace) =>
              Scaffold(body: Center(child: Text('Startup failed: $error'))),
      data:
          (loggedIn) =>
              loggedIn
                  ? const _PlaceholderHomeScreen()
                  : LoginScreen(
                    onLoggedIn: () => ref.invalidate(hasStoredSessionProvider),
                  ),
    );
  }
}

/// Stands in for the real landscape app shell (Phase 2+). Only exists so the
/// login flow has somewhere to land and can be logged out of for testing.
class _PlaceholderHomeScreen extends ConsumerWidget {
  const _PlaceholderHomeScreen();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(title: const Text('Purch.io')),
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            FilledButton(
              onPressed:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => const StaffListScreen()),
                  ),
              child: const Text('Manage Staff'),
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => const BranchListScreen()),
                  ),
              child: const Text('Manage Branches'),
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => const DeviceListScreen()),
                  ),
              child: const Text('Manage Devices'),
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(
                      builder: (_) => const TenantSettingsScreen(),
                    ),
                  ),
              child: const Text('Business Settings'),
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => const AuditLogScreen()),
                  ),
              child: const Text('Audit Log'),
            ),
            const SizedBox(height: 16),
            OutlinedButton(
              onPressed: () async {
                await ref.read(authRepositoryProvider).logout();
                ref.invalidate(hasStoredSessionProvider);
              },
              child: const Text('Log Out'),
            ),
          ],
        ),
      ),
    );
  }
}
