import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
      appBar: AppBar(title: const Text('Payment Due Reminders')),
      body: remindersAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load reminders: $error')),
        data: (reminders) {
          if (reminders.isEmpty) {
            return const Center(
              child: Text('No upcoming or overdue balances.'),
            );
          }

          return ListView.builder(
            itemCount: reminders.length,
            itemBuilder: (context, index) {
              final reminder = reminders[index];
              return ListTile(
                leading: Icon(
                  reminder.isOverdue ? Icons.error : Icons.schedule,
                  color: reminder.isOverdue ? Colors.red : null,
                ),
                title: Text(reminder.customerFullName),
                subtitle: Text(
                  '${reminder.customerPhoneNumber} — due '
                  '${reminder.dueDate.month}/${reminder.dueDate.day}/${reminder.dueDate.year}'
                  '${reminder.isOverdue ? ' (overdue)' : ''}',
                ),
                trailing: Text('₱${reminder.balance.toStringAsFixed(2)}'),
              );
            },
          );
        },
      ),
    );
  }
}
