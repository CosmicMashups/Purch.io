import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/routing/auth_gate.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../onboarding/domain/onboarding_enums.dart';
import '../../domain/incoming_receiving_models.dart';
import '../../domain/purchase_order_models.dart';
import '../providers/incoming_receiving_providers.dart';
import '../providers/purchase_order_providers.dart';
import 'incoming_receiving_form_screen.dart';

/// The Incoming Receiving Report page: every recorded delivery, who received
/// it, and whether it has been linked to a purchase order yet.
class IncomingReceivingListScreen extends ConsumerWidget {
  const IncomingReceivingListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final reportsAsync = ref.watch(incomingReceivingListProvider);
    final role = ref.watch(currentStaffRoleProvider).valueOrNull;
    final canLink = role == StaffRole.admin || role == StaffRole.manager;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Incoming Receiving Report')),
      body: reportsAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load receiving reports: ${describeError(error)}',
          onRetry: () => ref.read(incomingReceivingListProvider.notifier).refresh(),
        ),
        data: (reports) {
          if (reports.isEmpty) {
            return EmptyStateView(
              icon: Icons.inventory_2_outlined,
              title: 'No deliveries recorded yet — tap + to add one.',
              description:
                  'Record what arrived, its condition, and whether each item was accepted.',
              actionLabel: 'Record Delivery',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(builder: (_) => const IncomingReceivingFormScreen()),
              ),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh: () => ref.read(incomingReceivingListProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.md),
              itemCount: reports.length,
              separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) =>
                  _ReportCard(report: reports[index], canLink: canLink),
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => Navigator.of(context).push<void>(
          MaterialPageRoute(builder: (_) => const IncomingReceivingFormScreen()),
        ),
        tooltip: 'Record delivery',
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: AppColors.onBrandPrimary,
        child: const Icon(Icons.add),
      ),
    );
  }
}

class _ReportCard extends ConsumerWidget {
  const _ReportCard({required this.report, required this.canLink});

  final IncomingReceiving report;
  final bool canLink;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final linked = report.purchaseOrderId != null;
    final candidates =
        (ref.watch(purchaseOrderListProvider).valueOrNull ?? const <PurchaseOrder>[])
            .where(
              (o) =>
                  o.supplierId == report.supplierId &&
                  o.branchId == report.branchId &&
                  (o.status == PurchaseOrderStatus.sent ||
                      o.status == PurchaseOrderStatus.partiallyReceived),
            )
            .toList();
    final controller = ref.read(incomingReceivingControllerProvider.notifier);
    final failure = controller.currentFailure;

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.lgBorder,
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.subtle,
      ),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  report.supplierName,
                  style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: AppColors.textPrimary),
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: AppSpacing.sm, vertical: 4),
                decoration: BoxDecoration(
                  color: linked ? AppColors.accentEmeraldContainer : AppColors.accentWarmContainer,
                  borderRadius: BorderRadius.circular(AppRadius.full),
                ),
                child: Text(
                  linked ? 'Linked to PO' : 'Not linked',
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: linked ? AppColors.accentEmerald : AppColors.accentWarm,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 2),
          Text(
            'Delivered ${report.deliveryDate} to ${report.branchName}. Received by ${report.receivedByName}.',
            style: const TextStyle(fontSize: 13, color: AppColors.textSecondary),
          ),
          const SizedBox(height: AppSpacing.sm),
          for (final line in report.lines)
            Padding(
              padding: const EdgeInsets.only(bottom: 2),
              child: Text(
                '${line.itemName}: ${line.quantityReceived.toStringAsFixed(0)} ${line.uom} '
                '@ ${line.unitPrice.toStringAsFixed(2)} · ${line.condition.label} · ${line.remark.label}',
                style: const TextStyle(fontSize: 13, color: AppColors.textSecondary),
              ),
            ),
          if (failure != null) ...[
            const SizedBox(height: AppSpacing.sm),
            Text(failure.message, style: const TextStyle(color: AppColors.error, fontSize: 13)),
          ],
          if (!linked && canLink)
            Align(
              alignment: Alignment.centerRight,
              child: TextButton(
                onPressed: candidates.isEmpty
                    ? null
                    : () async {
                        final order = await showDialog<PurchaseOrder>(
                          context: context,
                          builder: (dialogContext) => SimpleDialog(
                            title: const Text('Link to purchase order'),
                            children: [
                              for (final order in candidates)
                                SimpleDialogOption(
                                  onPressed: () => Navigator.of(dialogContext).pop(order),
                                  child: Text(
                                    '${order.supplierName} → ${order.branchName}: '
                                    '${order.lines.map((l) => l.itemName).join(', ')}',
                                  ),
                                ),
                            ],
                          ),
                        );
                        if (order != null) {
                          await controller.link(report.id, order.id);
                        }
                      },
                child: Text(candidates.isEmpty ? 'No matching open order' : 'Link to purchase order'),
              ),
            ),
        ],
      ),
    );
  }
}
