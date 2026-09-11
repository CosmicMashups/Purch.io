import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
      appBar: AppBar(title: const Text('Audit Log')),
      body: logsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load the audit log: $error')),
        data: (logs) {
          if (logs.isEmpty) {
            return const Center(
              child: Text(
                'No sensitive actions recorded yet — this fills in as staff work.',
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(auditLogListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: logs.length,
              itemBuilder: (context, index) {
                final entry = logs[index];
                return ListTile(
                  leading: const Icon(Icons.history),
                  title: Text(_actionLabel(entry.actionType)),
                  subtitle: Text(
                    '${entry.targetEntityType} · ${entry.createdAt.toLocal()}',
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
