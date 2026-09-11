import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../sync_dto.dart';
import '../sync_providers.dart';

/// Admin/Manager surface for the "never silently dropped" promise — every
/// cross-device conflict the sync engine flags lands here for a human to
/// look at and acknowledge. See SyncService's doc comment on the backend.
class FlaggedSyncScreen extends ConsumerWidget {
  const FlaggedSyncScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final flaggedAsync = ref.watch(flaggedSyncRecordsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Sync Conflicts')),
      body: flaggedAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load conflicts: $error')),
        data: (records) {
          if (records.isEmpty) {
            return const Center(child: Text('No conflicts to review.'));
          }

          return RefreshIndicator(
            onRefresh:
                () => ref.read(flaggedSyncRecordsProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: records.length,
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

    return ListTile(
      leading: const CircleAvatar(child: Icon(Icons.warning_amber)),
      title: Text('${record.entityType} · ${record.entityId}'),
      subtitle: Text(
        'Lost the sync race — timestamped ${record.clientTimestamp.toLocal()}'
        '${isAcknowledged ? ' · acknowledged' : ''}',
      ),
      trailing:
          isAcknowledged
              ? const Icon(Icons.check_circle, color: Colors.green)
              : TextButton(
                onPressed:
                    controller.isLoading
                        ? null
                        : () async {
                          final succeeded = await ref
                              .read(
                                acknowledgeFlaggedControllerProvider.notifier,
                              )
                              .acknowledge(record.id);
                          if (!succeeded && context.mounted) {
                            final failure =
                                ref
                                    .read(
                                      acknowledgeFlaggedControllerProvider
                                          .notifier,
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
    );
  }
}
