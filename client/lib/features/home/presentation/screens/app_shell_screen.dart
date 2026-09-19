import '../../../pos/presentation/providers/pos_providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/auth/role_nav_policy.dart';
import '../../../../core/routing/auth_gate.dart';
import '../../../../core/sync/sync_providers.dart';
import '../../../../core/theming/app_tokens.dart';

/// Fixed branch order the router registers — index here must match the
/// `branches:` list in AppRouter exactly.
const List<AppTab> _branchOrder = [
  AppTab.home,
  AppTab.cashier,
  AppTab.inventory,
  AppTab.business,
];

/// The staff app shell: a persistent bottom [NavigationBar] over go_router's
/// [StatefulNavigationShell], so each tab keeps its own navigation stack.
/// Which tabs appear is role-dependent — see [tabsForRole] — so this widget
/// only ever renders a subset of [_branchOrder]'s four branches.
class AppShellScreen extends ConsumerWidget {
  const AppShellScreen({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Starts the background sync drain loop once, for the lifetime of the
    // logged-in session — keepAlive means later navigations reuse it.
    ref.watch(syncCoordinatorProvider);
    // Same for sales completed offline: keep trying to send them to the server.
    ref.watch(saleSyncCoordinatorProvider);

    final roleAsync = ref.watch(currentStaffRoleProvider);
    final role = roleAsync.value;
    final visibleTabs = tabsForRole(role);

    final currentTab = _branchOrder[navigationShell.currentIndex];
    var selectedIndex = visibleTabs.indexOf(currentTab);
    if (selectedIndex < 0) {
      selectedIndex = 0;
    }

    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: NavigationBarTheme(
        data: NavigationBarThemeData(
          backgroundColor: AppColors.surface,
          indicatorColor: AppColors.brandPrimaryContainer,
          labelTextStyle: WidgetStateProperty.resolveWith(
            (states) => TextStyle(
              fontSize: 12,
              fontWeight:
                  states.contains(WidgetState.selected)
                      ? FontWeight.w700
                      : FontWeight.w500,
              color:
                  states.contains(WidgetState.selected)
                      ? AppColors.brandPrimaryActive
                      : AppColors.textMuted,
            ),
          ),
          iconTheme: WidgetStateProperty.resolveWith(
            (states) => IconThemeData(
              color:
                  states.contains(WidgetState.selected)
                      ? AppColors.brandPrimaryActive
                      : AppColors.textMuted,
            ),
          ),
        ),
        child: NavigationBar(
          height: 64,
          elevation: 0,
          surfaceTintColor: Colors.transparent,
          selectedIndex: selectedIndex,
          onDestinationSelected: (index) {
            final tab = visibleTabs[index];
            navigationShell.goBranch(
              _branchOrder.indexOf(tab),
              initialLocation: tab == currentTab,
            );
          },
          destinations: [
            for (final tab in visibleTabs)
              NavigationDestination(
                icon: Icon(tabSpecFor(tab).icon),
                selectedIcon: Icon(tabSpecFor(tab).selectedIcon),
                label: tabSpecFor(tab).label,
              ),
          ],
        ),
      ),
    );
  }
}
