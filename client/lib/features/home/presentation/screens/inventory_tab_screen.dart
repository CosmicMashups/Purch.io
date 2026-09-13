import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/routing/nav_destinations.dart';
import '../../../../core/theming/app_tokens.dart';
import '../widgets/nav_tile_card.dart';

/// Inventory tab landing page — catalog, stock, and purchasing grouped into
/// their own sections rather than one long flat list.
class InventoryTabScreen extends StatelessWidget {
  const InventoryTabScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Inventory')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          AppSpacing.lg,
          AppSpacing.sm,
          AppSpacing.lg,
          AppSpacing.lg,
        ),
        children: [
          for (final section in inventorySections) ...[
            NavSectionHeader(title: section.title),
            for (final tile in section.tiles) ...[
              NavTileCard(
                icon: tile.icon,
                label: tile.label,
                subtitle: tile.subtitle,
                onTap: () => context.push('/inventory/${tile.path}'),
              ),
              const SizedBox(height: AppSpacing.md),
            ],
          ],
        ],
      ),
    );
  }
}
