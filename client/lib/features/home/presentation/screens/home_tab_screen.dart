import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/auth/role_nav_policy.dart';
import '../../../../core/routing/auth_gate.dart';
import '../../../../core/sync/sync_providers.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../credit_ledger/presentation/providers/credit_ledger_providers.dart';
import '../../../onboarding/domain/onboarding_enums.dart';
import '../widgets/nav_tile_card.dart';

/// Home tab: the first thing staff see after signing in — a big "New Sale"
/// launch action, a couple of quick shortcuts, and anything that needs their
/// attention (unsynced conflicts, overdue customer payments). Deliberately
/// light — full detail lives on the Sell/Reports/Business tabs; Home is a
/// launchpad, not another list.
class HomeTabScreen extends ConsumerWidget {
  const HomeTabScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final role = ref.watch(currentStaffRoleProvider).value;
    final syncConflicts = ref.watch(flaggedSyncRecordsProvider).value?.length ?? 0;
    final paymentReminders =
        role == StaffRole.cashier || role == StaffRole.warehouse
            ? 0
            : ref.watch(creditRemindersProvider()).value?.length ?? 0;

    return Scaffold(
      appBar: AppBar(title: const Text('Purch.io')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          Text(_greeting(), style: AppTypography.headlineSm),
          const SizedBox(height: 2),
          Text("Here's what's happening right now.", style: AppTypography.body),
          const SizedBox(height: AppSpacing.xl),
          _NewSaleCard(onTap: () => context.push('/home/new-sale')),
          const SizedBox(height: AppSpacing.xl),
          const NavSectionHeader(title: 'Quick actions'),
          Row(
            children: [
              Expanded(
                child: _QuickActionButton(
                  icon: Icons.point_of_sale_outlined,
                  label: 'Shift / Drawer',
                  onTap: () => context.push('/home/shift'),
                ),
              ),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: _QuickActionButton(
                  icon: Icons.notifications_active_outlined,
                  label: 'Payment Reminders',
                  badgeCount: paymentReminders,
                  onTap: () => context.push('/home/payment-reminders'),
                ),
              ),
            ],
          ),
          if (syncConflicts > 0) ...[
            const SizedBox(height: AppSpacing.xl),
            const NavSectionHeader(title: 'Needs your attention'),
            NavTileCard(
              icon: Icons.sync_problem_outlined,
              label: 'Sync Conflicts',
              subtitle: '$syncConflicts record${syncConflicts == 1 ? '' : 's'} waiting for review',
              iconColor: AppColors.error,
              iconBackground: const Color(0xFFFEE2E2),
              badgeCount: syncConflicts,
              onTap: () => context.push('/home/sync-conflicts'),
            ),
          ],
        ],
      ),
    );
  }

  String _greeting() {
    final hour = DateTime.now().hour;
    if (hour < 12) return 'Good morning';
    if (hour < 18) return 'Good afternoon';
    return 'Good evening';
  }
}

class _NewSaleCard extends StatelessWidget {
  const _NewSaleCard({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.brandPrimary,
      borderRadius: AppRadius.lgBorder,
      child: InkWell(
        borderRadius: AppRadius.lgBorder,
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.all(AppSpacing.xl),
          decoration: BoxDecoration(
            borderRadius: AppRadius.lgBorder,
            boxShadow: AppShadows.tactileButton,
          ),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      'New Sale',
                      style: AppTypography.headlineSm.copyWith(color: Colors.white),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Ring up an order',
                      style: AppTypography.body.copyWith(color: Colors.white.withValues(alpha: 0.85)),
                    ),
                  ],
                ),
              ),
              Container(
                width: 52,
                height: 52,
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.16),
                  borderRadius: AppRadius.mdBorder,
                ),
                child: const Icon(Icons.arrow_forward_rounded, color: Colors.white),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _QuickActionButton extends StatelessWidget {
  const _QuickActionButton({
    required this.icon,
    required this.label,
    required this.onTap,
    this.badgeCount = 0,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final int badgeCount;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.card,
      borderRadius: AppRadius.lgBorder,
      child: InkWell(
        borderRadius: AppRadius.lgBorder,
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: AppSpacing.lg, horizontal: AppSpacing.md),
          decoration: BoxDecoration(
            borderRadius: AppRadius.lgBorder,
            border: Border.all(color: AppColors.border),
          ),
          child: Column(
            children: [
              Stack(
                clipBehavior: Clip.none,
                children: [
                  Icon(icon, color: AppColors.brandPrimary, size: 26),
                  if (badgeCount > 0)
                    Positioned(
                      right: -8,
                      top: -6,
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                        decoration: BoxDecoration(
                          color: AppColors.error,
                          borderRadius: BorderRadius.circular(AppRadius.full),
                        ),
                        child: Text(
                          badgeCount > 99 ? '99+' : '$badgeCount',
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                    ),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),
              Text(
                label,
                textAlign: TextAlign.center,
                style: AppTypography.labelMd,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
