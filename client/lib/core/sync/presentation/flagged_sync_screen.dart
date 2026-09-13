import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../theming/app_tokens.dart';
import '../sync_dto.dart';
import '../sync_providers.dart';
import '../../widgets/empty_state_view.dart';
import '../../widgets/error_state_view.dart';

/// Admin/Manager surface for the "never silently dropped" promise — every
/// cross-device conflict the sync engine flags lands here for a human to
/// look at and acknowledge. See SyncService's doc comment on the backend.
class FlaggedSyncScreen extends ConsumerWidget {
  const FlaggedSyncScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final flaggedAsync = ref.watch(flaggedSyncRecordsProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Sync Conflicts'),
        elevation: 0,
      ),
      body: flaggedAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: error.toString(),
          onRetry: () =>
              ref.read(flaggedSyncRecordsProvider.notifier).refresh(),
        ),
        data: (records) {
          if (records.isEmpty) {
            return EmptyStateView(
              icon: Icons.cloud_done_outlined,
              title: 'No conflicts to review.',
              description:
                  'All offline changes, POS transactions, and inventory updates are synchronized cleanly across devices.',
              actionLabel: 'Check Status',
              onAction: () =>
                  ref.read(flaggedSyncRecordsProvider.notifier).refresh(),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh: () =>
                ref.read(flaggedSyncRecordsProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.md),
              itemCount: records.length,
              separatorBuilder: (context, index) =>
                  const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                final record = records[index];
                return _FlaggedRecordTile(record: record);
              },
            ),
          );
        },
      ),
    );
  }
}

class _FlaggedRecordTile extends ConsumerWidget {
  const _FlaggedRecordTile({required this.record});

  final FlaggedSyncRecord record;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final controller = ref.watch(acknowledgeFlaggedControllerProvider);
    final isAcknowledged = record.reviewedAt != null;

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border),
      ),
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.md,
          vertical: AppSpacing.xs,
        ),
        leading: Container(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: isAcknowledged
                ? AppColors.accentEmerald.withOpacity(0.12)
                : AppColors.accentWarm.withOpacity(0.12),
            borderRadius: AppRadius.smBorder,
          ),
          child: Icon(
            isAcknowledged
                ? Icons.check_circle_outline_rounded
                : Icons.warning_amber_rounded,
            color:
                isAcknowledged ? AppColors.accentEmerald : AppColors.accentWarm,
          ),
        ),
        title: Text(
          '${record.entityType} · ${record.entityId}',
          style: const TextStyle(
            fontSize: 15,
            fontWeight: FontWeight.w600,
            color: AppColors.textPrimary,
          ),
        ),
        subtitle: Padding(
          padding: const EdgeInsets.only(top: 4),
          child: Text(
            'Lost the sync race — timestamped ${record.clientTimestamp.toLocal()}'
            '${isAcknowledged ? ' · acknowledged' : ''}',
            style: const TextStyle(
              fontSize: 12,
              color: AppColors.textSecondary,
            ),
          ),
        ),
        trailing: isAcknowledged
            ? const Icon(
                Icons.check_circle,
                color: AppColors.accentEmerald,
              )
            : OutlinedButton(
                style: OutlinedButton.styleFrom(
                  side: const BorderSide(color: AppColors.brandPrimary),
                  shape: RoundedRectangleBorder(
                    borderRadius: AppRadius.smBorder,
                  ),
                ),
                onPressed: controller.isLoading
                    ? null
                    : () async {
                        final succeeded = await ref
                            .read(
                              acknowledgeFlaggedControllerProvider.notifier,
                            )
                            .acknowledge(record.id);
                        if (!succeeded && context.mounted) {
                          final failure = ref
                              .read(
                                acknowledgeFlaggedControllerProvider.notifier,
                              )
                              .currentFailure;
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                              content: Text(
                                failure?.message ??
                                    'Could not acknowledge this conflict.',
                              ),
                            ),
                          );
                        }
                      },
                child: const Text('Acknowledge'),
              ),
      ),
    );
  }
}

