import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/formatting/money.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../domain/credit_ledger_models.dart';
import '../providers/credit_ledger_providers.dart';
import 'add_customer_credit_ledger_screen.dart';

/// B7 — customer credit (utang) account management. Charging a sale to an
/// account happens through the POS's own Utang/Credit payment method at
/// checkout; this screen only manages accounts and takes repayments.
class CreditLedgerListScreen extends ConsumerWidget {
  const CreditLedgerListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ledgersAsync = ref.watch(creditLedgerListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Customer Accounts'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: ledgersAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) => ErrorStateView(
              message: error.toString(),
              onRetry:
                  () => ref.read(creditLedgerListProvider.notifier).refresh(),
            ),
        data: (ledgers) {
          if (ledgers.isEmpty) {
            return EmptyStateView(
              icon: Icons.account_balance_wallet_outlined,
              title: 'No customer accounts yet — tap + to add one.',
              description:
                  'Manage store credit (utang), set credit limits, track balances, and record customer repayments.',
              actionLabel: 'Add Customer Account',
              onAction:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(
                      builder: (_) => const AddCustomerCreditLedgerScreen(),
                    ),
                  ),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () => ref.read(creditLedgerListProvider.notifier).refresh(),
            child: ListView.builder(
              padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg, vertical: AppSpacing.md),
              itemCount: ledgers.length,
              itemBuilder: (context, index) {
                final ledger = ledgers[index];
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
                      backgroundColor: ledger.balance > 0 ? AppColors.accentWarmContainer : AppColors.cardHover,
                      foregroundColor: ledger.balance > 0 ? AppColors.accentWarm : AppColors.textMuted,
                      child: const Icon(Icons.person_outline, size: 20),
                    ),
                    title: Text(
                      ledger.customerFullName,
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: Text(
                      'Balance: ${formatCurrency(ledger.balance)} / '
                      '${formatCurrency(ledger.creditLimit)} limit'
                      '${ledger.dueDate != null ? ' — due ${ledger.dueDate!.month}/${ledger.dueDate!.day}/${ledger.dueDate!.year}' : ''}',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: ledger.balance > 0 ? AppColors.accentWarm : AppColors.textSecondary,
                        fontFeatures: const [FontFeature.tabularFigures()],
                      ),
                    ),
                    trailing:
                        ledger.balance > 0
                            ? TextButton(
                              onPressed:
                                  () => _showRecordPaymentDialog(
                                    context,
                                    ref,
                                    ledger,
                                  ),
                              style: TextButton.styleFrom(
                                foregroundColor: AppColors.brandPrimary,
                                textStyle: const TextStyle(fontWeight: FontWeight.w700),
                              ),
                              child: const Text('Record Payment'),
                            )
                            : null,
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
              MaterialPageRoute(
                builder: (_) => const AddCustomerCreditLedgerScreen(),
              ),
            ),
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: Colors.white,
        tooltip: 'Add customer',
        child: const Icon(Icons.add),
      ),
    );
  }

  Future<void> _showRecordPaymentDialog(
    BuildContext context,
    WidgetRef ref,
    CustomerCreditLedger ledger,
  ) async {
    final amountController = TextEditingController();
    final formKey = GlobalKey<FormState>();

    await showDialog<void>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
          title: Text('Record Payment — ${ledger.customerFullName}'),
          content: Form(
            key: formKey,
            child: TextFormField(
              controller: amountController,
              autofocus: true,
              decoration: const InputDecoration(
                labelText: 'Amount',
                prefixText: '₱ ',
                border: OutlineInputBorder(borderRadius: AppRadius.smBorder),
              ),
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              validator: (value) {
                final parsed = double.tryParse(value?.trim() ?? '');
                if (parsed == null || parsed <= 0) {
                  return 'Enter a valid amount';
                }
                if (parsed > ledger.balance) {
                  return 'Can\'t exceed the ${formatCurrency(ledger.balance)} balance';
                }
                return null;
              },
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () async {
                if (!(formKey.currentState?.validate() ?? false)) {
                  return;
                }
                final controller = ref.read(
                  recordCreditPaymentControllerProvider.notifier,
                );
                final succeeded = await controller.recordPayment(
                  ledger.id,
                  RecordCreditPaymentRequest(
                    amount: double.parse(amountController.text.trim()),
                  ),
                );
                if (succeeded && dialogContext.mounted) {
                  Navigator.of(dialogContext).pop();
                }
              },
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.brandPrimary,
                shape: const RoundedRectangleBorder(borderRadius: AppRadius.smBorder),
              ),
              child: const Text('Record'),
            ),
          ],
        );
      },
    );
  }
}
