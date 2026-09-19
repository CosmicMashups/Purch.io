import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../session/session_scope.dart';
import 'auth_gate.dart';
import '../../features/auth/presentation/screens/login_screen.dart';
import '../../features/catalog/presentation/screens/category_list_screen.dart';
import '../../features/catalog/presentation/screens/item_list_screen.dart';
import '../../features/catalog/presentation/screens/modifier_group_list_screen.dart';
import '../../features/credit_ledger/presentation/screens/credit_ledger_list_screen.dart';
import '../../features/credit_ledger/presentation/screens/credit_reminders_screen.dart';
import '../../features/home/presentation/screens/app_shell_screen.dart';
import '../../features/home/presentation/screens/business_tab_screen.dart';
import '../../features/home/presentation/screens/home_tab_screen.dart';
import '../../features/home/presentation/screens/inventory_tab_screen.dart';
import '../../features/inventory/presentation/screens/branch_transfer_list_screen.dart';
import '../../features/inventory/presentation/screens/movement_log_screen.dart';
import '../../features/inventory/presentation/screens/inventory_item_list_screen.dart';
import '../../features/inventory/presentation/screens/purchase_order_list_screen.dart';
import '../../features/inventory/presentation/screens/recipe_editor_screen.dart';
import '../../features/inventory/presentation/screens/supplier_list_screen.dart';
import '../../features/kiosk/presentation/screens/kiosk_landing_screen.dart';
import '../../features/kitchen_display/presentation/screens/kitchen_display_screen.dart';
import '../../features/order_board/presentation/screens/order_board_display_screen.dart';
import '../../features/onboarding/presentation/screens/audit_log_screen.dart';
import '../../features/onboarding/presentation/screens/branch_list_screen.dart';
import '../../features/onboarding/presentation/screens/device_list_screen.dart';
import '../../features/onboarding/presentation/screens/hardware_settings_screen.dart';
import '../../features/onboarding/presentation/screens/staff_list_screen.dart';
import '../../features/onboarding/presentation/screens/tenant_settings_screen.dart';
import '../hardware/cfd/customer_facing_display_screen.dart';
import '../../features/pos/presentation/screens/bir_reading_screen.dart';
import '../../features/pos/presentation/screens/cashier_screen.dart';
import '../../features/pos/presentation/screens/promos_screen.dart';
import '../../features/pos/presentation/screens/pending_kiosk_orders_screen.dart';
import '../../features/pos/presentation/screens/shift_screen.dart';
import '../../features/splash/presentation/screens/splash_screen.dart';
import '../sync/presentation/flagged_sync_screen.dart';
import '../theming/theme_builder.dart';

/// Notifies go_router to re-run its `redirect` whenever [authGateProvider]
/// changes — e.g. right after login or logout — since go_router itself has
/// no idea a Riverpod provider updated.
class _AuthRefreshNotifier extends ChangeNotifier {
  _AuthRefreshNotifier(Ref ref) {
    ref.listen(authGateProvider, (_, _) => notifyListeners());
  }
}

/// The one GoRouter for the whole app: a splash/login/kiosk top level, and a
/// [StatefulShellRoute] for the staff app shell's four (role-filtered)
/// bottom-nav tabs, each with its own navigation stack.
///
/// The Reports tab was removed: its four screens were flat text reports, and
/// Home now renders the same data as charts. The reports *domain/data* layer
/// is unchanged — Home consumes it directly.
final appRouterProvider = Provider<GoRouter>((ref) {
  final refresh = _AuthRefreshNotifier(ref);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: refresh,
    redirect: (context, state) {
      final location = state.matchedLocation;
      final gate = ref.read(authGateProvider);

      return gate.when(
        loading: () => location == '/splash' ? null : '/splash',
        error: (_, _) => location == '/splash' ? null : '/splash',
        data: (value) {
          switch (value) {
            case AuthGateState.loggedOut:
              return location == '/login' ? null : '/login';
            case AuthGateState.kiosk:
              return location.startsWith('/kiosk') ? null : '/kiosk';
            case AuthGateState.orderBoard:
              return location.startsWith('/order-board') ? null : '/order-board';
            case AuthGateState.kitchenDisplay:
              return location.startsWith('/kitchen-display') ? null : '/kitchen-display';
            case AuthGateState.staff:
              if (location == '/customer-facing-display') return null;
              final inStaffShell = _staffShellPaths.any(
                (path) => location.startsWith(path),
              );
              return inStaffShell ? null : '/home';
          }
        },
      );
    },
    routes: [
      GoRoute(path: '/splash', builder: (context, state) => const SplashScreen()),
      GoRoute(
        path: '/customer-facing-display',
        builder: (context, state) => const CustomerFacingDisplayScreen(),
      ),
      GoRoute(
        path: '/login',
        builder:
            (context, state) => LoginScreen(
              // A fresh login must never inherit the previous session's cached
              // data (possibly another tenant's), so rebuild every provider.
              onLoggedIn: resetSessionScope,
            ),
      ),
      GoRoute(
        path: '/kiosk',
        // The kiosk subtree runs the tactile customer-facing theme rather
        // than the staff one, rebuilt live from the tenant's branding.
        builder:
            (context, state) => Consumer(
              builder:
                  (context, ref, child) => Theme(
                    data: ref.watch(kioskThemeProvider),
                    child: child!,
                  ),
              child: const KioskLandingScreen(),
            ),
      ),
      GoRoute(
        path: '/order-board',
        builder: (context, state) => const OrderBoardDisplayScreen(),
      ),
      GoRoute(
        path: '/kitchen-display',
        builder: (context, state) => const KitchenDisplayScreen(),
      ),
      StatefulShellRoute.indexedStack(
        builder:
            (context, state, navigationShell) =>
                AppShellScreen(navigationShell: navigationShell),
        branches: [
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/home',
                builder: (context, state) => const HomeTabScreen(),
                routes: [
                  GoRoute(
                    path: 'new-sale',
                    builder: (context, state) => const CashierScreen(),
                  ),
                  GoRoute(
                    path: 'shift',
                    builder: (context, state) => const ShiftScreen(),
                  ),
                  GoRoute(
                    path: 'sync-conflicts',
                    builder: (context, state) => const FlaggedSyncScreen(),
                  ),
                  GoRoute(
                    path: 'payment-reminders',
                    builder: (context, state) => const CreditRemindersScreen(),
                  ),
                ],
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              // Renamed from `/sell` along with the tab label. A repo-wide
              // search found the old segment referenced only by this router
              // and the (now deleted) Sell landing screen — no deep links,
              // tests or stored state depended on it — so the path was
              // renamed with the label rather than left to drift.
              GoRoute(
                path: '/cashier',
                builder: (context, state) => const CashierScreen(),
                routes: [
                  GoRoute(
                    path: 'shift',
                    builder: (context, state) => const ShiftScreen(),
                  ),
                  GoRoute(
                    path: 'promo-codes',
                    builder: (context, state) => const PromosScreen(),
                  ),
                  GoRoute(
                    path: 'bir-reading',
                    builder: (context, state) => const BirReadingScreen(),
                  ),
                  GoRoute(
                    path: 'pending-kiosk-orders',
                    builder: (context, state) => const PendingKioskOrdersScreen(),
                  ),
                ],
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/inventory',
                builder: (context, state) => const InventoryTabScreen(),
                routes: [
                  GoRoute(
                    path: 'categories',
                    builder: (context, state) => const CategoryListScreen(),
                  ),
                  GoRoute(
                    path: 'items',
                    builder: (context, state) => const ItemListScreen(),
                  ),
                  GoRoute(
                    path: 'modifier-groups',
                    builder:
                        (context, state) => const ModifierGroupListScreen(),
                  ),
                  GoRoute(
                    path: 'movements',
                    builder: (context, state) => const MovementLogScreen(),
                  ),
                  GoRoute(
                    path: 'transfers',
                    builder:
                        (context, state) => const BranchTransferListScreen(),
                  ),
                  GoRoute(
                    path: 'suppliers',
                    builder: (context, state) => const SupplierListScreen(),
                  ),
                  GoRoute(
                    path: 'purchase-orders',
                    builder:
                        (context, state) => const PurchaseOrderListScreen(),
                  ),
                  GoRoute(
                    path: 'inventory-items',
                    builder:
                        (context, state) => const InventoryItemListScreen(),
                  ),
                  GoRoute(
                    path: 'items/:itemId/recipe',
                    builder: (context, state) {
                      final extra = state.extra;
                      final itemName =
                          extra is String ? extra : 'Item';
                      return RecipeEditorScreen(
                        itemId: state.pathParameters['itemId']!,
                        itemName: itemName,
                      );
                    },
                  ),
                ],
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/business',
                builder: (context, state) => const BusinessTabScreen(),
                routes: [
                  GoRoute(
                    path: 'customers',
                    builder: (context, state) => const CreditLedgerListScreen(),
                  ),
                  GoRoute(
                    path: 'payment-reminders',
                    builder: (context, state) => const CreditRemindersScreen(),
                  ),
                  GoRoute(
                    path: 'staff',
                    builder: (context, state) => const StaffListScreen(),
                  ),
                  GoRoute(
                    path: 'branches',
                    builder: (context, state) => const BranchListScreen(),
                  ),
                  GoRoute(
                    path: 'devices',
                    builder: (context, state) => const DeviceListScreen(),
                  ),
                  GoRoute(
                    path: 'hardware-settings',
                    builder: (context, state) => const HardwareSettingsScreen(),
                  ),
                  GoRoute(
                    path: 'settings',
                    builder: (context, state) => const TenantSettingsScreen(),
                  ),
                  GoRoute(
                    path: 'audit-log',
                    builder: (context, state) => const AuditLogScreen(),
                  ),
                  GoRoute(
                    path: 'sync-conflicts',
                    builder: (context, state) => const FlaggedSyncScreen(),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    ],
  );
});

const _staffShellPaths = ['/home', '/cashier', '/inventory', '/business'];
