import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/routing/nav_destinations.dart';
import '../../../../core/theming/app_tokens.dart';
import '../widgets/nav_tile_card.dart';

/// Reports tab landing page — the four analysis views (sales, inventory,
/// staff, department) a manager checks in on.
class ReportsTabScreen extends StatelessWidget {
  const ReportsTabScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Reports')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          for (final tile in reportsTiles) ...[
            NavTileCard(
              icon: tile.icon,
              label: tile.label,
              subtitle: tile.subtitle,
              iconColor: AppColors.accentEmerald,
              iconBackground: AppColors.accentEmeraldContainer,
              onTap: () => context.push('/reports/${tile.path}'),
            ),
            const SizedBox(height: AppSpacing.md),
          ],
        ],
      ),
    );
  }
}
