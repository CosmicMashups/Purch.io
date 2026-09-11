import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/catalog_providers.dart';
import 'receive_batch_screen.dart';

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
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load batches: $error')),
        data: (batches) {
          if (batches.isEmpty) {
            return const Center(
              child: Text('No stock received yet — tap + to receive some.'),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () =>
                    ref.read(itemBatchListProvider(itemId).notifier).refresh(),
            child: ListView.builder(
              itemCount: batches.length,
              itemBuilder: (context, index) {
                final batch = batches[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.inventory)),
                  title: Text('Lot ${batch.lotNumber}'),
                  subtitle: Text(
                    batch.expiryDate == null
                        ? '${batch.quantityRemaining} of ${batch.quantityReceived} remaining'
                        : '${batch.quantityRemaining} of ${batch.quantityReceived} remaining · '
                            'expires ${batch.expiryDate!.toLocal().toString().split(' ').first}',
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
        child: const Icon(Icons.add),
      ),
    );
  }
}
