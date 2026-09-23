import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/formatting/money.dart';
import '../../data/sale_queue.dart';
import '../../domain/offline_limits.dart';
import '../providers/pos_providers.dart';

/// Shown at the top of the Cashier while sales completed offline are waiting to
/// reach the server (or the server refused one): how many are waiting, how close
/// the terminal is to its offline selling limit, and a way in to review them.
/// Renders nothing when there is nothing to say.
class OfflineSalesBanner extends ConsumerWidget {
  const OfflineSalesBanner({super.key, this.limits = const OfflineLimits()});

  final OfflineLimits limits;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final entries = ref.watch(offlineSalesProvider).valueOrNull ?? const [];
    final stats = SaleQueueStats.of(entries);
    if (stats.isEmpty) {
      return const SizedBox.shrink();
    }

    final level = stats.level(limits, DateTime.now());
    final needsReview = stats.rejected > 0;
    final urgent = needsReview || level == OfflineLevel.blocked;
    final warning = level == OfflineLevel.warning;

    final color =
        urgent
            ? AppColors.error
            : warning
            ? AppColors.accentWarm
            : AppColors.textSecondary;

    final String message;
    if (level == OfflineLevel.blocked) {
      message =
          'Offline selling limit reached — reconnect to sync '
          '${stats.unsynced} waiting sale${stats.unsynced == 1 ? '' : 's'} '
          'before selling more.';
    } else if (needsReview) {
      message =
          '${stats.rejected} offline sale${stats.rejected == 1 ? '' : 's'} '
          'could not be recorded and need${stats.rejected == 1 ? 's' : ''} review.';
    } else if (warning) {
      message =
          '${stats.unsynced} sale${stats.unsynced == 1 ? '' : 's'} waiting to '
          'sync — reconnect soon (limit ${limits.maxSales} sales or '
          '${limits.maxAge.inHours} hours).';
    } else {
      message =
          '${stats.unsynced} sale${stats.unsynced == 1 ? '' : 's'} saved offline, '
          'syncing when the connection returns.';
    }

    return Semantics(
      liveRegion: true,
      button: true,
      label: message,
      child: Material(
        color: color.withValues(alpha: 0.12),
        child: InkWell(
          onTap: () => _showSales(context),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            child: Row(
              children: [
                Icon(
                  urgent ? Icons.error_outline_rounded : Icons.cloud_off_rounded,
                  size: 18,
                  color: color,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    message,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: color,
                    ),
                  ),
                ),
                Icon(Icons.chevron_right_rounded, size: 18, color: color),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _showSales(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => const OfflineSalesSheet(),
    );
  }
}

/// The offline sales waiting to sync or needing review, with a "Sync now"
/// button and — for a sale the server refused — the reason and a way to mark
/// it as seen.
class OfflineSalesSheet extends ConsumerWidget {
  const OfflineSalesSheet({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final entries = ref.watch(offlineSalesProvider).valueOrNull ?? const [];

    return SafeArea(
      child: ConstrainedBox(
        constraints: BoxConstraints(
          maxHeight: MediaQuery.sizeOf(context).height * 0.75,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 8),
              child: Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Offline sales',
                      style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
                    ),
                  ),
                  FilledButton.icon(
                    onPressed:
                        entries.isEmpty
                            ? null
                            : () => ref.read(saleSyncCoordinatorProvider).drain(),
                    icon: const Icon(Icons.sync_rounded, size: 18),
                    label: const Text('Sync now'),
                  ),
                ],
              ),
            ),
            if (entries.isEmpty)
              const Padding(
                padding: EdgeInsets.all(32),
                child: Center(child: Text('Everything has been synced.')),
              )
            else
              Flexible(
                child: ListView.separated(
                  shrinkWrap: true,
                  padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
                  itemCount: entries.length,
                  separatorBuilder: (_, __) => const Divider(height: 1),
                  itemBuilder: (context, index) => _SaleTile(entry: entries[index]),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _SaleTile extends ConsumerWidget {
  const _SaleTile({required this.entry});

  final SaleQueueEntry entry;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final rejected = entry.status == SaleQueueStatus.rejected;
    final time = TimeOfDay.fromDateTime(entry.soldAt).format(context);

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: Icon(
        rejected ? Icons.error_outline_rounded : Icons.schedule_rounded,
        color: rejected ? AppColors.error : AppColors.textSecondary,
      ),
      title: Text(
        'Receipt No. ${entry.receiptNumber} · ${formatCurrency(entry.totalAmount)}',
        style: const TextStyle(fontWeight: FontWeight.w700),
      ),
      subtitle: Text(
        rejected
            ? 'Sold $time — not recorded: ${entry.lastError ?? 'refused by the server'}'
            : 'Sold $time — waiting to sync'
                '${entry.attempts > 0 ? ' (tried ${entry.attempts}×)' : ''}',
      ),
      trailing:
          rejected
              ? TextButton(
                onPressed:
                    () => ref
                        .read(saleQueueStoreProvider)
                        .dismiss(entry.saleId),
                child: const Text('Mark seen'),
              )
              : null,
    );
  }
}
