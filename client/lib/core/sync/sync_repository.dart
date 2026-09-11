import 'sync_dto.dart';

/// Talks to the backend /sync endpoints — see docs/adr/0003. This is
/// deliberately generic: it just moves batches of already-serialized items
/// and flagged-record DTOs back and forth, with no knowledge of what a
/// "Transaction" or "Item" entity actually is.
abstract class SyncRepository {
  Future<SyncBatchResult> syncBatch(List<SyncItemRequest> items);

  Future<List<FlaggedSyncRecord>> listFlagged();

  Future<FlaggedSyncRecord> acknowledgeFlagged(String syncedRecordId);
}
