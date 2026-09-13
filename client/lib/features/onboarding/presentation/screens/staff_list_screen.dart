import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../domain/onboarding_enums.dart';
import '../providers/onboarding_providers.dart';
import 'add_staff_screen.dart';

/// A4's staff list — one entry per staff member, role and active status
/// visible at a glance, per the design brief's "icon-forward, minimal text"
/// direction. Deactivating/reactivating (not deleting) matches Purch.LoginService's
/// active-users-only PIN check without needing a separate delete concept.
class StaffListScreen extends ConsumerWidget {
  const StaffListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final staffAsync = ref.watch(staffListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Staff'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: staffAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) => ErrorStateView(
              message: error.toString(),
              onRetry: () => ref.read(staffListProvider.notifier).refresh(),
            ),
        data: (staff) {
          if (staff.isEmpty) {
            return EmptyStateView(
              icon: Icons.people_outline,
              title: 'No staff yet — tap + to add your first one.',
              description:
                  'Invite team members, assign POS or manager roles, and configure secure PINs for quick shift logins.',
              actionLabel: 'Invite Staff',
              onAction:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => const AddStaffScreen()),
                  ),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(staffListProvider.notifier).refresh(),
            child: ListView.builder(
              padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg, vertical: AppSpacing.md),
              itemCount: staff.length,
              itemBuilder: (context, index) {
                final member = staff[index];
                return Card(
                  elevation: 0,
                  color: AppColors.surface,
                  margin: const EdgeInsets.only(bottom: AppSpacing.sm),
                  shape: const RoundedRectangleBorder(
                    borderRadius: AppRadius.mdBorder,
                    side: BorderSide(color: AppColors.border),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg, vertical: AppSpacing.xs),
                    leading: CircleAvatar(
                      backgroundColor: member.isActive ? AppColors.brandPrimaryContainer : AppColors.cardHover,
                      foregroundColor: member.isActive ? AppColors.brandPrimary : AppColors.textMuted,
                      child: Icon(
                        member.isActive ? Icons.person : Icons.person_off,
                        size: 20,
                      ),
                    ),
                    title: Text(
                      member.name,
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        fontWeight: FontWeight.w600,
                        color: member.isActive ? AppColors.textPrimary : AppColors.textMuted,
                      ),
                    ),
                    subtitle: Text(
                      _roleLabel(member.role),
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
                    ),
                    trailing:
                        member.isActive
                            ? Container(
                              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                              decoration: BoxDecoration(
                                color: AppColors.accentEmeraldContainer,
                                borderRadius: BorderRadius.circular(AppRadius.full),
                              ),
                              child: Text(
                                'Active',
                                style: Theme.of(context).textTheme.labelSmall?.copyWith(
                                  color: AppColors.accentEmerald,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            )
                            : const Chip(
                              label: Text('Inactive'),
                              visualDensity: VisualDensity.compact,
                            ),
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const AddStaffScreen()),
            ),
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: Colors.white,
        tooltip: 'Add staff',
        child: const Icon(Icons.add),
      ),
    );
  }

  String _roleLabel(StaffRole role) => switch (role) {
    StaffRole.admin => 'Admin',
    StaffRole.manager => 'Manager',
    StaffRole.cashier => 'Cashier',
    StaffRole.warehouse => 'Warehouse',
  };
}
