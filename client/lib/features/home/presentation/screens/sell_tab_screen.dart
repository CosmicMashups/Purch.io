import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/routing/nav_destinations.dart';
import '../../../../core/theming/app_tokens.dart';
import '../widgets/nav_tile_card.dart';

/// Sell tab landing page — POS operations: ringing up sales, cash drawer,
/// promos, and the BIR-mandated X/Z readings.
class SellTabScreen extends StatelessWidget {
  const SellTabScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Sell')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          for (final tile in sellTiles) ...[
            NavTileCard(
              icon: tile.icon,
              label: tile.label,
              subtitle: tile.subtitle,
              onTap: () => context.push('/sell/${tile.path}'),
            ),
            const SizedBox(height: AppSpacing.md),
          ],
        ],
      ),
    );
  }
}
