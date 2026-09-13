import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../domain/purchase_order_models.dart';
import '../providers/purchase_order_providers.dart';
import 'create_purchase_order_screen.dart';
import 'receive_purchase_order_screen.dart';

/// C5 — the PO list with its status tracker (Draft → Sent → Partially
/// Received/Received, or Cancelled from Draft/Sent).
class PurchaseOrderListScreen extends ConsumerWidget {
  const PurchaseOrderListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final purchaseOrdersAsync = ref.watch(purchaseOrderListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Purchase Orders')),
      body: purchaseOrdersAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load purchase orders: $error',
          onRetry: () =>
              ref.read(purchaseOrderListProvider.notifier).refresh(),
        ),
        data: (purchaseOrders) {
          if (purchaseOrders.isEmpty) {
            return EmptyStateView(
              icon: Icons.local_shipping_outlined,
              title: 'No purchase orders yet — tap + to create one.',
              description:
                  'Track incoming stock orders, supplier lead times, and receiving status.',
              actionLabel: 'Create Purchase Order',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => const CreatePurchaseOrderScreen(),
                ),
              ),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh:
                () => ref.read(purchaseOrderListProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.md),
              itemCount: purchaseOrders.length,
              separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                return _PurchaseOrderCard(purchaseOrder: purchaseOrders[index]);
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(
                builder: (_) => const CreatePurchaseOrderScreen(),
              ),
            ),
        tooltip: 'New purchase order',
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: AppColors.onBrandPrimary,
        child: const Icon(Icons.add),
      ),
    );
  }
}

class _PurchaseOrderCard extends ConsumerWidget {
  const _PurchaseOrderCard({required this.purchaseOrder});

  final PurchaseOrder purchaseOrder;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final actionState = ref.watch(
      purchaseOrderActionControllerProvider(purchaseOrder.id),
    );
    final isLoading = actionState.isLoading;
    final failure =
        ref
            .read(
              purchaseOrderActionControllerProvider(purchaseOrder.id).notifier,
            )
            .currentFailure;
    final controller = ref.read(
      purchaseOrderActionControllerProvider(purchaseOrder.id).notifier,
    );

    final canCancel =
        purchaseOrder.status == PurchaseOrderStatus.draft ||
        purchaseOrder.status == PurchaseOrderStatus.sent;

    final (statusColor, statusBg) = switch (purchaseOrder.status) {
      PurchaseOrderStatus.received => (AppColors.accentEmerald, AppColors.accentEmeraldContainer),
      PurchaseOrderStatus.sent || PurchaseOrderStatus.partiallyReceived => (AppColors.accentWarm, AppColors.accentWarmContainer),
      PurchaseOrderStatus.cancelled => (AppColors.error, AppColors.cardHover),
      _ => (AppColors.textSecondary, AppColors.cardHover),
    };

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
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Text(
                  '${purchaseOrder.supplierName} → ${purchaseOrder.branchName}',
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.sm,
                  vertical: 4,
                ),
                decoration: BoxDecoration(
                  color: statusBg,
                  borderRadius: BorderRadius.circular(AppRadius.full),
                ),
                child: Text(
                  purchaseOrder.status.label,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: statusColor,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          for (final line in purchaseOrder.lines)
            Padding(
              padding: const EdgeInsets.only(bottom: 2),
              child: Text(
                '${line.itemName}: ${line.quantityReceived.toStringAsFixed(0)} / '
                '${line.quantityOrdered.toStringAsFixed(0)} received',
                style: const TextStyle(
                  fontSize: 13,
                  color: AppColors.textSecondary,
                  fontFeatures: [FontFeature.tabularFigures()],
                ),
              ),
            ),
          if (failure != null) ...[
            const SizedBox(height: AppSpacing.sm),
            Container(
              padding: const EdgeInsets.all(AppSpacing.xs),
              decoration: BoxDecoration(
                color: AppColors.cardHover,
                borderRadius: AppRadius.mdBorder,
                border: Border.all(color: AppColors.error),
              ),
              child: Text(
                failure.message,
                style: const TextStyle(color: AppColors.error, fontSize: 13),
              ),
            ),
          ],
          const SizedBox(height: AppSpacing.sm),
          Wrap(
            alignment: WrapAlignment.end,
            spacing: AppSpacing.sm,
            children: [
              if (canCancel)
                TextButton(
                  style: TextButton.styleFrom(
                    foregroundColor: AppColors.error,
                  ),
                  onPressed: isLoading ? null : controller.cancel,
                  child: const Text('Cancel'),
                ),
              if (purchaseOrder.status == PurchaseOrderStatus.draft)
                FilledButton(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.brandPrimary,
                    foregroundColor: AppColors.onBrandPrimary,
                    shape: const RoundedRectangleBorder(
                      borderRadius: AppRadius.mdBorder,
                    ),
                  ),
                  onPressed: isLoading ? null : controller.markSent,
                  child: const Text('Mark Sent'),
                ),
              if (purchaseOrder.status == PurchaseOrderStatus.sent ||
                  purchaseOrder.status ==
                      PurchaseOrderStatus.partiallyReceived)
                FilledButton(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.accentEmerald,
                    foregroundColor: AppColors.onBrandPrimary,
                    shape: const RoundedRectangleBorder(
                      borderRadius: AppRadius.mdBorder,
                    ),
                  ),
                  onPressed:
                      isLoading
                          ? null
                          : () => Navigator.of(context).push<void>(
                            MaterialPageRoute(
                              builder:
                                  (_) => ReceivePurchaseOrderScreen(
                                    purchaseOrder: purchaseOrder,
                                  ),
                            ),
                          ),
                  child: const Text('Receive'),
                ),
            ],
          ),
        ],
      ),
    );
  }
}
