import 'dart:math';

import 'package:drift/drift.dart';

import '../app_database.dart';
import '../tables/pending_sync_queue_table.dart';

part 'sync_queue_dao.g.dart';

/// NFR3's durability boundary — see PendingSyncQueue's doc comment. Every
/// offline-capable write calls [enqueue] first; SyncCoordinator drains the
/// queue in batches once connectivity is restored.
@DriftAccessor(tables: [PendingSyncQueue])
class SyncQueueDao extends DatabaseAccessor<AppDatabase>
    with _$SyncQueueDaoMixin {
  SyncQueueDao(super.db);

  static final _random = Random.secure();

  /// A client-generated id that doubles as the /sync idempotency key —
  /// unique enough per device without pulling in a uuid package for it.
  static String _generateId() {
    final timestamp = DateTime.now().microsecondsSinceEpoch;
    final suffix = _random.nextInt(1 << 32).toRadixString(16).padLeft(8, '0');
    return '$timestamp-$suffix';
  }

  Future<String> enqueue({
    required String entityType,
    required String entityId,
    required String payloadJson,
  }) async {
    final id = _generateId();
    await into(pendingSyncQueue).insert(
      PendingSyncQueueCompanion.insert(
        id: id,
        entityType: entityType,
        entityId: entityId,
        payloadJson: payloadJson,
        createdAt: DateTime.now(),
      ),
    );
    return id;
  }

  /// Rows still needing a sync attempt — pending (never tried) or failed
  /// (a previous attempt didn't get a server response, safe to retry since
  /// the idempotency key makes a retry-after-partial-failure harmless).
  Future<List<PendingSyncQueueData>> listPending() {
    return (select(pendingSyncQueue)
          ..where((row) => row.syncStatus.isIn(['pending', 'failed']))
          ..orderBy([(row) => OrderingTerm.asc(row.createdAt)]))
        .get();
  }

  Future<void> markSyncing(List<String> ids) => _setStatus(ids, 'syncing');

  Future<void> markSynced(List<String> ids) => _setStatus(ids, 'synced');

  Future<void> markFailed(List<String> ids) => _setStatus(ids, 'failed');

  Future<void> _setStatus(List<String> ids, String status) async {
    if (ids.isEmpty) {
      return;
    }
    await (update(pendingSyncQueue)..where(
      (row) => row.id.isIn(ids),
    )).write(PendingSyncQueueCompanion(syncStatus: Value(status)));
  }
}
