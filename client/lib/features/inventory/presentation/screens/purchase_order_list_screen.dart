import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
      appBar: AppBar(title: const Text('Purchase Orders')),
      body: purchaseOrdersAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load purchase orders: $error')),
        data: (purchaseOrders) {
          if (purchaseOrders.isEmpty) {
            return const Center(
              child: Text('No purchase orders yet — tap + to create one.'),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () => ref.read(purchaseOrderListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: purchaseOrders.length,
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

    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Text(
                    '${purchaseOrder.supplierName} → ${purchaseOrder.branchName}',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                Chip(label: Text(purchaseOrder.status.label)),
              ],
            ),
            const SizedBox(height: 8),
            for (final line in purchaseOrder.lines)
              Text(
                '${line.itemName}: ${line.quantityReceived.toStringAsFixed(0)} / '
                '${line.quantityOrdered.toStringAsFixed(0)} received',
              ),
            if (failure != null) ...[
              const SizedBox(height: 8),
              Text(
                failure.message,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
            const SizedBox(height: 8),
            Wrap(
              alignment: WrapAlignment.end,
              spacing: 8,
              children: [
                if (canCancel)
                  TextButton(
                    onPressed: isLoading ? null : controller.cancel,
                    child: const Text('Cancel'),
                  ),
                if (purchaseOrder.status == PurchaseOrderStatus.draft)
                  FilledButton(
                    onPressed: isLoading ? null : controller.markSent,
                    child: const Text('Mark Sent'),
                  ),
                if (purchaseOrder.status == PurchaseOrderStatus.sent ||
                    purchaseOrder.status ==
                        PurchaseOrderStatus.partiallyReceived)
                  FilledButton(
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
      ),
    );
  }
}
