import 'package:flutter/material.dart';

import '../../features/onboarding/domain/onboarding_enums.dart';

/// The four sections of the staff app shell's bottom navigation. Not every
/// role sees every tab — see [tabsForRole].
///
/// `reports` was removed with the Reports tab: Home now carries that
/// reporting surface as charts, and it is gated inside Home by role rather
/// than by a tab of its own.
enum AppTab { home, cashier, inventory, business }

class TabSpec {
  const TabSpec({
    required this.tab,
    required this.label,
    required this.icon,
    required this.selectedIcon,
  });

  final AppTab tab;
  final String label;
  final IconData icon;
  final IconData selectedIcon;
}

const Map<AppTab, TabSpec> _tabSpecs = {
  AppTab.home: TabSpec(
    tab: AppTab.home,
    label: 'Home',
    icon: Icons.home_outlined,
    selectedIcon: Icons.home_rounded,
  ),
  AppTab.cashier: TabSpec(
    tab: AppTab.cashier,
    label: 'Cashier',
    icon: Icons.point_of_sale_outlined,
    selectedIcon: Icons.point_of_sale_rounded,
  ),
  AppTab.inventory: TabSpec(
    tab: AppTab.inventory,
    label: 'Inventory',
    icon: Icons.inventory_2_outlined,
    selectedIcon: Icons.inventory_2_rounded,
  ),
  AppTab.business: TabSpec(
    tab: AppTab.business,
    label: 'Business',
    icon: Icons.storefront_outlined,
    selectedIcon: Icons.storefront_rounded,
  ),
};

TabSpec tabSpecFor(AppTab tab) => _tabSpecs[tab]!;

/// Which bottom-nav tabs a signed-in staff member sees, by role. A `null`
/// role (claim missing/unparseable) gets the most restrictive set rather
/// than the most permissive one.
List<AppTab> tabsForRole(StaffRole? role) {
  switch (role) {
    case StaffRole.admin:
    case StaffRole.manager:
      return const [
        AppTab.home,
        AppTab.cashier,
        AppTab.inventory,
        AppTab.business,
      ];
    case StaffRole.cashier:
      return const [AppTab.home, AppTab.cashier];
    case StaffRole.warehouse:
      return const [AppTab.home, AppTab.inventory];
    case null:
      return const [AppTab.home, AppTab.cashier];
  }
}

/// Business-tab tiles gate further by role: Admin sees everything, Manager
/// loses the two Admin-only settings tiles, and no other role reaches the
/// Business tab at all (see [tabsForRole]).
bool isBusinessTileVisible(String tileId, StaffRole? role) {
  if (role == StaffRole.admin) {
    return true;
  }
  if (role == StaffRole.manager) {
    return tileId != 'devices' && tileId != 'business-settings';
  }
  return false;
}

/// Case-insensitive match against the raw JWT `role` claim (e.g. "Admin",
/// "Cashier") back to [StaffRole] — the claim is the backend's C# enum name.
StaffRole? staffRoleFromClaim(String? claim) {
  if (claim == null) {
    return null;
  }
  for (final role in StaffRole.values) {
    if (role.name.toLowerCase() == claim.toLowerCase()) {
      return role;
    }
  }
  return null;
}
