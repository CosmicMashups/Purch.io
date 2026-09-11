import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';

import '../db/app_database.dart';
import '../db/daos/sync_queue_dao.dart';
import 'sync_dto.dart';
import 'sync_repository.dart';

/// Drains PendingSyncQueue to the backend whenever connectivity is restored.
/// Deliberately does NOT retrofit any feature's live write path to enqueue
/// through here yet — that's a separate, unstarted follow-up. This class
/// only owns draining whatever ends up in the queue.
class SyncCoordinator {
  SyncCoordinator({
    required SyncQueueDao syncQueueDao,
    required SyncRepository syncRepository,
    Connectivity? connectivity,
  }) : _syncQueueDao = syncQueueDao,
       _syncRepository = syncRepository,
       _connectivity = connectivity ?? Connectivity();

  static const _batchSize = 25;

  final SyncQueueDao _syncQueueDao;
  final SyncRepository _syncRepository;
  final Connectivity _connectivity;
  StreamSubscription<List<ConnectivityResult>>? _subscription;
  bool _draining = false;

  void start() {
    _subscription ??= _connectivity.onConnectivityChanged.listen((results) {
      if (!results.contains(ConnectivityResult.none)) {
        unawaited(drainQueue());
      }
    });
    unawaited(drainQueue());
  }

  void dispose() {
    unawaited(_subscription?.cancel());
    _subscription = null;
  }

  /// Sends every pending/failed row to the backend in batches. Safe to call
  /// concurrently or repeatedly — re-entrant calls are no-ops while a drain
  /// is already in flight, and each item's idempotency key makes a retried
  /// row harmless even if a prior attempt actually succeeded server-side.
  Future<void> drainQueue() async {
    if (_draining) {
      return;
    }
    _draining = true;
    try {
      final pending = await _syncQueueDao.listPending();
      for (var offset = 0; offset < pending.length; offset += _batchSize) {
        final batch = pending.skip(offset).take(_batchSize).toList();
        await _syncBatch(batch);
      }
    } finally {
      _draining = false;
    }
  }

  Future<void> _syncBatch(List<PendingSyncQueueData> batch) async {
    final ids = batch.map((row) => row.id).toList();
    await _syncQueueDao.markSyncing(ids);

    final SyncBatchResult result;
    try {
      result = await _syncRepository.syncBatch(
        batch
            .map(
              (row) => SyncItemRequest(
                idempotencyKey: row.id,
                entityType: row.entityType,
                entityId: row.entityId,
                clientTimestamp: row.createdAt,
                payloadJson: row.payloadJson,
              ),
            )
            .toList(),
      );
    } catch (_) {
      // Transport/server failure — leave for the next drain to retry.
      await _syncQueueDao.markFailed(ids);
      return;
    }

    // Applied, AlreadySynced, and ConflictFlagged are all "the server has
    // durably recorded this" outcomes — only a caught exception above means
    // retry. A flagged conflict is not silently dropped; see /sync/flagged.
    final byKey = {
      for (final item in result.results) item.idempotencyKey: item,
    };
    final synced = ids.where(byKey.containsKey).toList();
    final unaccountedFor = ids.where((id) => !byKey.containsKey(id)).toList();
    await _syncQueueDao.markSynced(synced);
    await _syncQueueDao.markFailed(unaccountedFor);
  }
}
