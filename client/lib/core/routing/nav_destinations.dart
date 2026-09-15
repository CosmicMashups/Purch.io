import 'package:flutter/material.dart';

import '../auth/role_nav_policy.dart';
import '../../features/onboarding/domain/onboarding_enums.dart';

/// One destination tile shown on a tab's landing page. [path] is relative to
/// that tab's branch root (e.g. Cashier tab + path `promo-codes` resolves to
/// `/cashier/promo-codes`) — see the router.
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

// The Cashier tab has no tile list: its landing page *is* the item grid plus
// the cart, and Shift / Promo Codes / X-Z Reading are compact secondary
// buttons on that screen rather than full-width tiles.
//
// The Reports tab and its `reportsTiles` were removed entirely — Home now
// renders that data as charts off the same reports repository.

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
