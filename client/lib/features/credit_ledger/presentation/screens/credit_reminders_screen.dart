import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/credit_ledger_providers.dart';

/// B7's due-date reminders — ledgers with a balance due within (or already
/// past) a lookahead window. No SMS/email vendor is chosen yet, so this is a
/// list the app displays rather than a push notification.
class CreditRemindersScreen extends ConsumerWidget {
  const CreditRemindersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final remindersAsync = ref.watch(creditRemindersProvider());

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Payment Due Reminders'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
      ),
      body: remindersAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error:
            (error, stackTrace) => ErrorStateView(
              message: error.toString(),
              onRetry: () => ref.refresh(creditRemindersProvider()),
            ),
        data: (reminders) {
          if (reminders.isEmpty) {
            return EmptyStateView(
              icon: Icons.check_circle_outline,
              title: 'No upcoming or overdue balances.',
              description:
                  'All customer credit accounts are settled or within their agreed credit terms.',
              actionLabel: 'Refresh',
              onAction: () => ref.refresh(creditRemindersProvider()),
            );
          }

          return ListView.separated(
            padding: const EdgeInsets.all(AppSpacing.lg),
            itemCount: reminders.length,
            separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
            itemBuilder: (context, index) {
              final reminder = reminders[index];
              return Card(
                elevation: 0,
                color: AppColors.surface,
                shape: RoundedRectangleBorder(
                  borderRadius: AppRadius.mdBorder,
                  side: BorderSide(
                    color: reminder.isOverdue
                        ? AppColors.accentWarm.withValues(alpha: 0.5)
                        : AppColors.border,
                  ),
                ),
                child: ListTile(
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: AppSpacing.md,
                    vertical: AppSpacing.xs,
                  ),
                  leading: Container(
                    padding: const EdgeInsets.all(AppSpacing.sm),
                    decoration: BoxDecoration(
                      color: reminder.isOverdue
                          ? AppColors.accentWarm.withValues(alpha: 0.1)
                          : AppColors.brandPrimaryContainer,
                      borderRadius: AppRadius.mdBorder,
                    ),
                    child: Icon(
                      reminder.isOverdue ? Icons.error : Icons.schedule,
                      color: reminder.isOverdue
                          ? AppColors.accentWarm
                          : AppColors.brandPrimary,
                      size: 22,
                    ),
                  ),
                  title: Text(
                    reminder.customerFullName,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  subtitle: Padding(
                    padding: const EdgeInsets.only(top: 2),
                    child: Text(
                      '${reminder.customerPhoneNumber} — due '
                      '${reminder.dueDate.month}/${reminder.dueDate.day}/${reminder.dueDate.year}'
                      '${reminder.isOverdue ? ' (overdue)' : ''}',
                      style: TextStyle(
                        color: reminder.isOverdue
                            ? AppColors.accentWarm
                            : AppColors.textSecondary,
                        fontSize: 13,
                        fontWeight: reminder.isOverdue
                            ? FontWeight.w600
                            : FontWeight.normal,
                      ),
                    ),
                  ),
                  trailing: Text(
                    '₱${reminder.balance.toStringAsFixed(2)}',
                    style: TextStyle(
                      fontFeatures: const [FontFeature.tabularFigures()],
                      fontWeight: FontWeight.w700,
                      fontSize: 16,
                      color: reminder.isOverdue
                          ? AppColors.accentWarm
                          : AppColors.textPrimary,
                    ),
                  ),
                ),
              );
            },
          );
        },
      ),
    );
  }
}
