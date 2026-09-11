import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
      appBar: AppBar(title: const Text('Customer Accounts')),
      body: ledgersAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load customer accounts: $error')),
        data: (ledgers) {
          if (ledgers.isEmpty) {
            return const Center(
              child: Text('No customer accounts yet — tap + to add one.'),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () => ref.read(creditLedgerListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: ledgers.length,
              itemBuilder: (context, index) {
                final ledger = ledgers[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.person)),
                  title: Text(ledger.customerFullName),
                  subtitle: Text(
                    'Balance: ₱${ledger.balance.toStringAsFixed(2)} / '
                    '₱${ledger.creditLimit.toStringAsFixed(2)} limit'
                    '${ledger.dueDate != null ? ' — due ${ledger.dueDate!.month}/${ledger.dueDate!.day}/${ledger.dueDate!.year}' : ''}',
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
                            child: const Text('Record Payment'),
                          )
                          : null,
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
          title: Text('Record Payment — ${ledger.customerFullName}'),
          content: Form(
            key: formKey,
            child: TextFormField(
              controller: amountController,
              autofocus: true,
              decoration: const InputDecoration(
                labelText: 'Amount',
                prefixText: '₱ ',
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
                  return 'Can\'t exceed the ₱${ledger.balance.toStringAsFixed(2)} balance';
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
              child: const Text('Record'),
            ),
          ],
        );
      },
    );
  }
}
