import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/catalog_providers.dart';
import 'receive_batch_screen.dart';
import '../../../../core/errors/failure.dart';

/// B2a — weight/volume batch/lot tracking. Only reachable for items whose
/// pricingType is weightVolume (see ItemListScreen), matching the backend's
/// own rejection of batches against any other pricing type.
class ItemBatchesScreen extends ConsumerWidget {
  const ItemBatchesScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final batchesAsync = ref.watch(itemBatchListProvider(itemId));

    return Scaffold(
      appBar: AppBar(title: Text('Batches: $itemName')),
      body: batchesAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load batches: ${describeError(error)}',
          onRetry: () =>
              ref.read(itemBatchListProvider(itemId).notifier).refresh(),
        ),
        data: (batches) {
          if (batches.isEmpty) {
            return EmptyStateView(
              icon: Icons.inventory_2_outlined,
              title: 'No stock received yet — tap + to receive some.',
              description:
                  'Track expiration dates and batch numbers for freshness and FIFO inventory handling.',
              actionLabel: 'Receive Stock',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) =>
                      ReceiveBatchScreen(itemId: itemId, itemName: itemName),
                ),
              ),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh:
                () =>
                    ref.read(itemBatchListProvider(itemId).notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.md),
              itemCount: batches.length,
              separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                final batch = batches[index];
                return Container(
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: AppRadius.lgBorder,
                    border: Border.all(color: AppColors.border),
                    boxShadow: AppShadows.subtle,
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.all(AppSpacing.md),
                    leading: Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        borderRadius: BorderRadius.circular(AppRadius.full),
                      ),
                      child: const Icon(
                        Icons.inventory,
                        size: 22,
                        color: AppColors.brandPrimary,
                      ),
                    ),
                    title: Text(
                      'Lot ${batch.lotNumber}',
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 16,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text(
                        batch.expiryDate == null
                            ? '${batch.quantityRemaining} of ${batch.quantityReceived} remaining'
                            : '${batch.quantityRemaining} of ${batch.quantityReceived} remaining · '
                                'expires ${batch.expiryDate!.toLocal().toString().split(' ').first}',
                        style: const TextStyle(
                          fontSize: 13,
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
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(
                builder:
                    (_) =>
                        ReceiveBatchScreen(itemId: itemId, itemName: itemName),
              ),
            ),
        tooltip: 'Receive stock',
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: AppColors.onBrandPrimary,
        child: const Icon(Icons.add),
      ),
    );
  }
}
