import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/routing/auth_gate.dart';
import '../../../../core/routing/nav_destinations.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/purch_app_bar.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../widgets/nav_tile_card.dart';

/// Business tab landing page — customers, staff/branches/devices, and
/// configuration, sectioned and filtered by role (Manager loses the two
/// Admin-only configuration tiles — see [businessSectionsForRole]). Also
/// hosts the Log Out action, since it no longer has a dedicated nav slot.
class BusinessTabScreen extends ConsumerWidget {
  const BusinessTabScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final role = ref.watch(currentStaffRoleProvider).value;
    final sections = businessSectionsForRole(role);

    return Scaffold(
      appBar: PurchAppBar(
        title: 'Business',
        actions: [
          IconButton(
            tooltip: 'Log Out',
            icon: const Icon(Icons.logout_rounded),
            onPressed: () => _confirmLogout(context, ref),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          AppSpacing.lg,
          AppSpacing.sm,
          AppSpacing.lg,
          AppSpacing.lg,
        ),
        children: [
          for (final section in sections) ...[
            NavSectionHeader(title: section.title),
            for (final tile in section.tiles) ...[
              NavTileCard(
                icon: tile.icon,
                label: tile.label,
                subtitle: tile.subtitle,
                iconColor: AppColors.accentWarm,
                iconBackground: AppColors.accentWarmContainer,
                onTap: () => context.push('/business/${tile.path}'),
              ),
              const SizedBox(height: AppSpacing.md),
            ],
          ],
        ],
      ),
    );
  }

  Future<void> _confirmLogout(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder:
          (dialogContext) => AlertDialog(
            title: const Text('Log out?'),
            content: const Text('You will need your pairing code and PIN to sign back in.'),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(false),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: () => Navigator.of(dialogContext).pop(true),
                child: const Text('Log Out'),
              ),
            ],
          ),
    );

    if (confirmed ?? false) {
      await ref.read(authRepositoryProvider).logout();
      ref.invalidate(hasStoredSessionProvider);
      ref.invalidate(storedSessionRoleProvider);
    }
  }
}
