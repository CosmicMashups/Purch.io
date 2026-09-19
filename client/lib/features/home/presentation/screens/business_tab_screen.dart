import '../../../pos/data/sale_queue.dart';
import '../../../pos/presentation/providers/pos_providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/routing/auth_gate.dart';
import '../../../../core/routing/nav_destinations.dart';
import '../../../../core/session/session_scope.dart';
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
    // Sales completed offline are only on this device until they sync, and they
    // can't sync while signed out — say so before it's too late to matter.
    final waiting = SaleQueueStats.of(
      ref.read(offlineSalesProvider).valueOrNull ?? const [],
    ).unsynced;

    final confirmed = await showDialog<bool>(
      context: context,
      builder:
          (dialogContext) => AlertDialog(
            title: const Text('Log out?'),
            content: Text(
              waiting == 0
                  ? 'You will need your pairing code and PIN to sign back in.'
                  : '$waiting sale${waiting == 1 ? ' is' : 's are'} saved on this '
                      'device and not yet sent to the server. They will stay here '
                      'and sync the next time this terminal is signed in with '
                      'a connection. Log out anyway?',
            ),
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
      // Drop every cached provider so the next login starts clean.
      resetSessionScope();
    }
  }
}
