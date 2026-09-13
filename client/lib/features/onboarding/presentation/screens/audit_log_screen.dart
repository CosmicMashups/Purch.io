import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../domain/audit_log_models.dart';
import '../providers/onboarding_providers.dart';

/// A6's audit log viewer. Filtering by staff/date/action type is deferred —
/// see the provider's doc comment — this shows the full tenant history,
/// most recent first (already sorted server-side).
class AuditLogScreen extends ConsumerWidget {
  const AuditLogScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final logsAsync = ref.watch(auditLogListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Audit Log'),
        elevation: 0,
      ),
      body: logsAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: error.toString(),
          onRetry: () => ref.read(auditLogListProvider.notifier).refresh(),
        ),
        data: (logs) {
          if (logs.isEmpty) {
            return EmptyStateView(
              icon: Icons.history_toggle_off_rounded,
              title:
                  'No sensitive actions recorded yet — this fills in as staff work.',
              description:
                  'Track voids, refunds, manual drawer opens, price overrides, and adjustments as your team operates.',
              actionLabel: 'Refresh',
              onAction: () => ref.read(auditLogListProvider.notifier).refresh(),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh: () => ref.read(auditLogListProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.md),
              itemCount: logs.length,
              separatorBuilder: (context, index) =>
                  const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                final entry = logs[index];
                return Container(
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: AppRadius.mdBorder,
                    border: Border.all(color: AppColors.border),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.xs,
                    ),
                    leading: Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: AppColors.accentWarm.withOpacity(0.12),
                        borderRadius: AppRadius.smBorder,
                      ),
                      child: const Icon(
                        Icons.shield_outlined,
                        color: AppColors.accentWarm,
                      ),
                    ),
                    title: Text(
                      _actionLabel(entry.actionType),
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text(
                        '${entry.targetEntityType} · ${entry.createdAt.toLocal()}',
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }

  String _actionLabel(AuditActionType type) => switch (type) {
    AuditActionType.void_ => 'Void',
    AuditActionType.refund => 'Refund',
    AuditActionType.discountOverride => 'Discount Override',
    AuditActionType.priceOverride => 'Price Override',
    AuditActionType.inventoryAdjustment => 'Inventory Adjustment',
    AuditActionType.departmentReassignment => 'Department Reassignment',
    AuditActionType.creditLimitOverride => 'Credit Limit Override',
    AuditActionType.cashDrawerManualOpen => 'Cash Drawer Manual Open',
  };
}

