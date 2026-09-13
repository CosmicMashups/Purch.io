import 'package:flutter/material.dart';

import '../auth/role_nav_policy.dart';
import '../../features/onboarding/domain/onboarding_enums.dart';

/// One destination tile shown on a tab's landing page. [path] is relative to
/// that tab's branch root (e.g. `sell` tab + path `promo-codes` resolves to
/// `/sell/promo-codes`) — see [AppRouter].
class NavTile {
  const NavTile({
    required this.id,
    required this.label,
    required this.icon,
    required this.path,
    this.subtitle,
  });

  final String id;
  final String label;
  final IconData icon;
  final String path;
  final String? subtitle;
}

/// A labeled cluster of tiles within a tab (e.g. Inventory's "Catalog" vs.
/// "Stock" vs. "Purchasing" groups) so dense tabs read as organized sections
/// rather than another flat list.
class NavSection {
  const NavSection({required this.title, required this.tiles});

  final String title;
  final List<NavTile> tiles;
}

const List<NavTile> sellTiles = [
  NavTile(
    id: 'new-sale',
    label: 'New Sale',
    icon: Icons.point_of_sale_rounded,
    path: 'new-sale',
    subtitle: 'Ring up an order',
  ),
  NavTile(
    id: 'shift',
    label: 'Shift / Cash Drawer',
    icon: Icons.point_of_sale_outlined,
    path: 'shift',
    subtitle: 'Open, close, and count',
  ),
  NavTile(
    id: 'promo-codes',
    label: 'Promo Codes',
    icon: Icons.local_offer_outlined,
    path: 'promo-codes',
    subtitle: 'Discounts and vouchers',
  ),
  NavTile(
    id: 'bir-reading',
    label: 'X / Z Reading',
    icon: Icons.receipt_long_outlined,
    path: 'bir-reading',
    subtitle: 'BIR-compliant readings',
  ),
];

const List<NavTile> reportsTiles = [
  NavTile(
    id: 'sales-dashboard',
    label: 'Sales Dashboard',
    icon: Icons.query_stats_rounded,
    path: 'sales-dashboard',
    subtitle: 'Revenue at a glance',
  ),
  NavTile(
    id: 'inventory-reports',
    label: 'Inventory Reports',
    icon: Icons.bar_chart_rounded,
    path: 'inventory-reports',
    subtitle: 'Stock trends and valuation',
  ),
  NavTile(
    id: 'staff-performance',
    label: 'Staff Performance',
    icon: Icons.leaderboard_outlined,
    path: 'staff-performance',
    subtitle: 'Sales by staff member',
  ),
  NavTile(
    id: 'department-sales',
    label: 'Department Sales',
    icon: Icons.pie_chart_outline_rounded,
    path: 'department-sales',
    subtitle: 'Revenue by department',
  ),
];

const List<NavSection> inventorySections = [
  NavSection(
    title: 'Catalog',
    tiles: [
      NavTile(
        id: 'categories',
        label: 'Manage Categories',
        icon: Icons.category_outlined,
        path: 'categories',
      ),
      NavTile(
        id: 'items',
        label: 'Manage Items',
        icon: Icons.inventory_outlined,
        path: 'items',
      ),
      NavTile(
        id: 'modifier-groups',
        label: 'Manage Modifier Groups',
        icon: Icons.tune_rounded,
        path: 'modifier-groups',
      ),
    ],
  ),
  NavSection(
    title: 'Stock',
    tiles: [
      NavTile(
        id: 'inventory-dashboard',
        label: 'Inventory Dashboard',
        icon: Icons.dashboard_outlined,
        path: 'dashboard',
      ),
      NavTile(
        id: 'movements',
        label: 'Stock Movements',
        icon: Icons.swap_vert_rounded,
        path: 'movements',
      ),
      NavTile(
        id: 'transfers',
        label: 'Stock Transfers',
        icon: Icons.compare_arrows_rounded,
        path: 'transfers',
      ),
    ],
  ),
  NavSection(
    title: 'Purchasing',
    tiles: [
      NavTile(
        id: 'suppliers',
        label: 'Suppliers',
        icon: Icons.local_shipping_outlined,
        path: 'suppliers',
      ),
      NavTile(
        id: 'purchase-orders',
        label: 'Purchase Orders',
        icon: Icons.assignment_outlined,
        path: 'purchase-orders',
      ),
    ],
  ),
];

/// Business tab tiles, before role filtering — see [isBusinessTileVisible].
const List<NavSection> businessSectionsUnfiltered = [
  NavSection(
    title: 'Customers',
    tiles: [
      NavTile(
        id: 'customers',
        label: 'Manage Customer Accounts',
        icon: Icons.people_alt_outlined,
        path: 'customers',
      ),
      NavTile(
        id: 'payment-reminders',
        label: 'Payment Due Reminders',
        icon: Icons.notifications_active_outlined,
        path: 'payment-reminders',
      ),
    ],
  ),
  NavSection(
    title: 'People & Branches',
    tiles: [
      NavTile(
        id: 'staff',
        label: 'Manage Staff',
        icon: Icons.badge_outlined,
        path: 'staff',
      ),
      NavTile(
        id: 'branches',
        label: 'Manage Branches',
        icon: Icons.store_mall_directory_outlined,
        path: 'branches',
      ),
      NavTile(
        id: 'devices',
        label: 'Manage Devices',
        icon: Icons.tablet_mac_outlined,
        path: 'devices',
      ),
    ],
  ),
  NavSection(
    title: 'Configuration',
    tiles: [
      NavTile(
        id: 'business-settings',
        label: 'Business Settings',
        icon: Icons.settings_outlined,
        path: 'settings',
      ),
      NavTile(
        id: 'audit-log',
        label: 'Audit Log',
        icon: Icons.fact_check_outlined,
        path: 'audit-log',
      ),
      NavTile(
        id: 'sync-conflicts',
        label: 'Sync Conflicts',
        icon: Icons.sync_problem_outlined,
        path: 'sync-conflicts',
      ),
    ],
  ),
];

List<NavSection> businessSectionsForRole(StaffRole? role) {
  return businessSectionsUnfiltered
      .map(
        (section) => NavSection(
          title: section.title,
          tiles:
              section.tiles
                  .where((tile) => isBusinessTileVisible(tile.id, role))
                  .toList(),
        ),
      )
      .where((section) => section.tiles.isNotEmpty)
      .toList();
}
