import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/branch_transfer_models.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/branch_transfer_providers.dart';
import 'create_branch_transfer_screen.dart';
import '../../../../core/errors/failure.dart';

/// C5's branch transfer list.
class BranchTransferListScreen extends ConsumerWidget {
  const BranchTransferListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final transfersAsync = ref.watch(branchTransferListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Stock Transfers'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: transfersAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load transfers: ${describeError(error)}',
          onRetry: () =>
              ref.read(branchTransferListProvider.notifier).refresh(),
        ),
        data: (transfers) {
          if (transfers.isEmpty) {
            return EmptyStateView(
              icon: Icons.sync_alt,
              title: 'No transfers yet — tap + to start one.',
              description:
                  'Safely route inventory between branches with dispatch and receive confirmation.',
              actionLabel: 'New Stock Transfer',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => const CreateBranchTransferScreen(),
                ),
              ),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () => ref.read(branchTransferListProvider.notifier).refresh(),
            child: ListView.builder(
              padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg, vertical: AppSpacing.md),
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
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: Colors.white,
        tooltip: 'New transfer',
        child: const Icon(Icons.add),
      ),
    );
  }
}

class _TransferCard extends ConsumerWidget {
  const _TransferCard({required this.transfer});

  final BranchTransfer transfer;

  Color _statusColor(BranchTransferStatus status) {
    switch (status) {
      case BranchTransferStatus.pending:
        return AppColors.accentWarm;
      case BranchTransferStatus.inTransit:
        return AppColors.brandPrimary;
      case BranchTransferStatus.received:
        return AppColors.accentEmerald;
      case BranchTransferStatus.cancelled:
        return AppColors.textMuted;
    }
  }

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
    final color = _statusColor(transfer.status);

    return Card(
      elevation: 0,
      color: AppColors.surface,
      margin: const EdgeInsets.only(bottom: AppSpacing.md),
      shape: const RoundedRectangleBorder(
        borderRadius: AppRadius.mdBorder,
        side: BorderSide(color: AppColors.border),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Text(
                    '${transfer.sourceBranchName} → ${transfer.destinationBranchName}',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w600,
                    ),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(AppRadius.full),
                  ),
                  child: Text(
                    transfer.status.label,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: color,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.md),
            for (final line in transfer.lines)
              Padding(
                padding: const EdgeInsets.only(bottom: 4),
                child: Row(
                  children: [
                    const Icon(Icons.inventory_2_outlined, size: 14, color: AppColors.textMuted),
                    const SizedBox(width: AppSpacing.xs),
                    Expanded(
                      child: Text(
                        line.itemName,
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: AppColors.textPrimary),
                      ),
                    ),
                    Text(
                      '× ${line.quantity.toStringAsFixed(0)}',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                        fontWeight: FontWeight.w600,
                        fontFeatures: const [FontFeature.tabularFigures()],
                      ),
                    ),
                  ],
                ),
              ),
            if (failure != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text(
                failure.message,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.error),
              ),
            ],
            if (transfer.status == BranchTransferStatus.pending ||
                transfer.status == BranchTransferStatus.inTransit) ...[
              const SizedBox(height: AppSpacing.md),
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
                  style: FilledButton.styleFrom(
                    backgroundColor: transfer.status == BranchTransferStatus.pending
                        ? AppColors.brandPrimary
                        : AppColors.accentEmerald,
                    shape: const RoundedRectangleBorder(
                      borderRadius: AppRadius.smBorder,
                    ),
                  ),
                  child: Text(
                    transfer.status == BranchTransferStatus.pending
                        ? 'Mark In Transit'
                        : 'Mark Received',
                  ),
                ),
              ),
              // Calling it off is always possible until it has arrived; shipped stock is put back.
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed:
                      isLoading
                          ? null
                          : () =>
                              ref
                                  .read(
                                    branchTransferActionControllerProvider(
                                      transfer.id,
                                    ).notifier,
                                  )
                                  .cancel(),
                  child: const Text('Cancel'),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
