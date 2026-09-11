import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/sync/presentation/flagged_sync_screen.dart';
import 'core/sync/sync_providers.dart';
import 'features/auth/presentation/providers/auth_providers.dart';
import 'features/auth/presentation/screens/login_screen.dart';
import 'features/catalog/presentation/screens/category_list_screen.dart';
import 'features/credit_ledger/presentation/screens/credit_ledger_list_screen.dart';
import 'features/credit_ledger/presentation/screens/credit_reminders_screen.dart';
import 'features/catalog/presentation/screens/item_list_screen.dart';
import 'features/catalog/presentation/screens/modifier_group_list_screen.dart';
import 'features/inventory/presentation/screens/branch_transfer_list_screen.dart';
import 'features/inventory/presentation/screens/inventory_dashboard_screen.dart';
import 'features/inventory/presentation/screens/movement_log_screen.dart';
import 'features/inventory/presentation/screens/purchase_order_list_screen.dart';
import 'features/inventory/presentation/screens/supplier_list_screen.dart';
import 'features/kiosk/presentation/screens/kiosk_landing_screen.dart';
import 'features/onboarding/presentation/screens/audit_log_screen.dart';
import 'features/onboarding/presentation/screens/branch_list_screen.dart';
import 'features/onboarding/presentation/screens/device_list_screen.dart';
import 'features/onboarding/presentation/screens/staff_list_screen.dart';
import 'features/onboarding/presentation/screens/tenant_settings_screen.dart';
import 'features/pos/presentation/screens/bir_reading_screen.dart';
import 'features/pos/presentation/screens/item_grid_screen.dart';
import 'features/pos/presentation/screens/promo_code_list_screen.dart';
import 'features/pos/presentation/screens/shift_screen.dart';
import 'features/reports/presentation/screens/department_sales_screen.dart';
import 'features/reports/presentation/screens/inventory_reports_screen.dart';
import 'features/reports/presentation/screens/sales_dashboard_screen.dart';
import 'features/reports/presentation/screens/staff_performance_screen.dart';

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
    final role = ref.watch(storedSessionRoleProvider);

    void reevaluateSession() {
      ref.invalidate(hasStoredSessionProvider);
      ref.invalidate(storedSessionRoleProvider);
    }

    return hasSession.when(
      loading:
          () =>
              const Scaffold(body: Center(child: CircularProgressIndicator())),
      error:
          (error, stackTrace) =>
              Scaffold(body: Center(child: Text('Startup failed: $error'))),
      data: (loggedIn) {
        if (!loggedIn) {
          return LoginScreen(onLoggedIn: reevaluateSession);
        }

        // A Kiosk-role token routes to the portrait kiosk shell instead of
        // the staff app shell — a separate route tree entirely, see Phase 7.
        return role.when(
          loading:
              () => const Scaffold(
                body: Center(child: CircularProgressIndicator()),
              ),
          error:
              (error, stackTrace) =>
                  Scaffold(body: Center(child: Text('Startup failed: $error'))),
          data:
              (roleValue) =>
                  roleValue == 'Kiosk'
                      ? const KioskLandingScreen()
                      : const _PlaceholderHomeScreen(),
        );
      },
    );
  }
}

/// Stands in for the real landscape app shell (Phase 2+). Only exists so the
/// login flow has somewhere to land and can be logged out of for testing.
class _PlaceholderHomeScreen extends ConsumerWidget {
  const _PlaceholderHomeScreen();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Starts the background sync drain loop once, for the lifetime of the
    // logged-in session — keepAlive means later navigations reuse it.
    ref.watch(syncCoordinatorProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Purch.io')),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.symmetric(vertical: 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(builder: (_) => const ItemGridScreen()),
                    ),
                child: const Text('New Sale'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(builder: (_) => const ShiftScreen()),
                    ),
                child: const Text('Shift / Cash Drawer'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const PromoCodeListScreen(),
                      ),
                    ),
                child: const Text('Promo Codes'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const BirReadingScreen(),
                      ),
                    ),
                child: const Text('X / Z Reading'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const SalesDashboardScreen(),
                      ),
                    ),
                child: const Text('Sales Dashboard'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const InventoryReportsScreen(),
                      ),
                    ),
                child: const Text('Inventory Reports'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const StaffPerformanceScreen(),
                      ),
                    ),
                child: const Text('Staff Performance'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const DepartmentSalesScreen(),
                      ),
                    ),
                child: const Text('Department Sales'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const CreditLedgerListScreen(),
                      ),
                    ),
                child: const Text('Manage Customer Accounts'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const CreditRemindersScreen(),
                      ),
                    ),
                child: const Text('Payment Due Reminders'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const StaffListScreen(),
                      ),
                    ),
                child: const Text('Manage Staff'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const BranchListScreen(),
                      ),
                    ),
                child: const Text('Manage Branches'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const DeviceListScreen(),
                      ),
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
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const CategoryListScreen(),
                      ),
                    ),
                child: const Text('Manage Categories'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(builder: (_) => const ItemListScreen()),
                    ),
                child: const Text('Manage Items'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const InventoryDashboardScreen(),
                      ),
                    ),
                child: const Text('Inventory Dashboard'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const MovementLogScreen(),
                      ),
                    ),
                child: const Text('Stock Movements'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const BranchTransferListScreen(),
                      ),
                    ),
                child: const Text('Stock Transfers'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const SupplierListScreen(),
                      ),
                    ),
                child: const Text('Suppliers'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const PurchaseOrderListScreen(),
                      ),
                    ),
                child: const Text('Purchase Orders'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const ModifierGroupListScreen(),
                      ),
                    ),
                child: const Text('Manage Modifier Groups'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed:
                    () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => const FlaggedSyncScreen(),
                      ),
                    ),
                child: const Text('Sync Conflicts'),
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
      ),
    );
  }
}
