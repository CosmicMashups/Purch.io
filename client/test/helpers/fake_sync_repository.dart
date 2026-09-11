import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/core/sync/sync_dto.dart';
import 'package:purch_client/core/sync/sync_repository.dart';

class FakeSyncRepository implements SyncRepository {
  FakeSyncRepository({this.failureToThrow});

  final Failure? failureToThrow;
  final List<FlaggedSyncRecord> flaggedRecords = [];
  List<SyncItemRequest> lastBatch = [];

  /// Override to control what syncBatch returns per call; defaults to
  /// marking every item Applied.
  SyncBatchResult Function(List<SyncItemRequest> items)? syncBatchHandler;

  @override
  Future<SyncBatchResult> syncBatch(List<SyncItemRequest> items) async {
    lastBatch = items;
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    if (syncBatchHandler != null) {
      return syncBatchHandler!(items);
    }
    return SyncBatchResult(
      results:
          items
              .map(
                (item) => SyncItemResult(
                  idempotencyKey: item.idempotencyKey,
                  status: SyncItemStatus.applied,
                  message: 'Recorded.',
                ),
              )
              .toList(),
    );
  }

  @override
  Future<List<FlaggedSyncRecord>> listFlagged() async {
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    return flaggedRecords;
  }

  @override
  Future<FlaggedSyncRecord> acknowledgeFlagged(String syncedRecordId) async {
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    final index = flaggedRecords.indexWhere(
      (record) => record.id == syncedRecordId,
    );
    final acknowledged = FlaggedSyncRecord(
      id: flaggedRecords[index].id,
      deviceId: flaggedRecords[index].deviceId,
      entityType: flaggedRecords[index].entityType,
      entityId: flaggedRecords[index].entityId,
      clientTimestamp: flaggedRecords[index].clientTimestamp,
      reviewedAt: DateTime.utc(2026, 1, 1),
    );
    flaggedRecords[index] = acknowledged;
    return acknowledged;
  }
}
