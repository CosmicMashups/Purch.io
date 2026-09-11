import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/branch_transfer_models.dart';
import '../providers/branch_transfer_providers.dart';
import 'create_branch_transfer_screen.dart';

/// C4 — multi-branch stock transfer list with its Pending → In Transit →
/// Received status tracker.
class BranchTransferListScreen extends ConsumerWidget {
  const BranchTransferListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final transfersAsync = ref.watch(branchTransferListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Stock Transfers')),
      body: transfersAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load transfers: $error')),
        data: (transfers) {
          if (transfers.isEmpty) {
            return const Center(
              child: Text('No transfers yet — tap + to start one.'),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () => ref.read(branchTransferListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: transfers.length,
              itemBuilder: (context, index) {
                return _TransferCard(transfer: transfers[index]);
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(
                builder: (_) => const CreateBranchTransferScreen(),
              ),
            ),
        tooltip: 'New transfer',
        child: const Icon(Icons.add),
      ),
    );
  }
}

class _TransferCard extends ConsumerWidget {
  const _TransferCard({required this.transfer});

  final BranchTransfer transfer;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final actionState = ref.watch(
      branchTransferActionControllerProvider(transfer.id),
    );
    final isLoading = actionState.isLoading;
    final failure =
        ref
            .read(branchTransferActionControllerProvider(transfer.id).notifier)
            .currentFailure;

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
                    '${transfer.sourceBranchName} → ${transfer.destinationBranchName}',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                Chip(label: Text(transfer.status.label)),
              ],
            ),
            const SizedBox(height: 8),
            for (final line in transfer.lines)
              Text('${line.itemName} × ${line.quantity.toStringAsFixed(0)}'),
            if (failure != null) ...[
              const SizedBox(height: 8),
              Text(
                failure.message,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
            if (transfer.status != BranchTransferStatus.received) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerRight,
                child: FilledButton(
                  onPressed:
                      isLoading
                          ? null
                          : () {
                            final controller = ref.read(
                              branchTransferActionControllerProvider(
                                transfer.id,
                              ).notifier,
                            );
                            if (transfer.status ==
                                BranchTransferStatus.pending) {
                              controller.markInTransit();
                            } else {
                              controller.markReceived();
                            }
                          },
                  child: Text(
                    transfer.status == BranchTransferStatus.pending
                        ? 'Mark In Transit'
                        : 'Mark Received',
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
