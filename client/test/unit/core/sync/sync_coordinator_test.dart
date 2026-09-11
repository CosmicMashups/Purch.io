import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/db/app_database.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/core/sync/sync_coordinator.dart';
import 'package:purch_client/core/sync/sync_dto.dart';

import '../../../helpers/fake_sync_repository.dart';

void main() {
  group('SyncCoordinator', () {
    late AppDatabase database;

    setUp(() {
      database = AppDatabase.forTesting(NativeDatabase.memory());
    });

    tearDown(() async {
      await database.close();
    });

    test('drains pending rows and marks them synced on success', () async {
      final dao = database.syncQueueDao;
      final id = await dao.enqueue(
        entityType: 'Transaction',
        entityId: 'txn-1',
        payloadJson: '{}',
      );
      final repository = FakeSyncRepository();
      final coordinator = SyncCoordinator(
        syncQueueDao: dao,
        syncRepository: repository,
      );

      await coordinator.drainQueue();

      expect(await dao.listPending(), isEmpty);
      expect(repository.lastBatch.single.idempotencyKey, id);
    });

    test('a conflict-flagged item still counts as synced from the client'
        ' perspective', () async {
      final dao = database.syncQueueDao;
      final id = await dao.enqueue(
        entityType: 'Item',
        entityId: 'item-1',
        payloadJson: '{}',
      );
      final repository =
          FakeSyncRepository()
            ..syncBatchHandler =
                (items) => SyncBatchResult(
                  results: [
                    SyncItemResult(
                      idempotencyKey: items.single.idempotencyKey,
                      status: SyncItemStatus.conflictFlagged,
                      message: 'flagged',
                    ),
                  ],
                );
      final coordinator = SyncCoordinator(
        syncQueueDao: dao,
        syncRepository: repository,
      );

      await coordinator.drainQueue();

      expect(await dao.listPending(), isEmpty);
      expect(repository.lastBatch.single.idempotencyKey, id);
    });

    test('a transport failure leaves the row for the next drain', () async {
      final dao = database.syncQueueDao;
      await dao.enqueue(
        entityType: 'Transaction',
        entityId: 'txn-1',
        payloadJson: '{}',
      );
      final repository = FakeSyncRepository(
        failureToThrow: const NetworkFailure('offline'),
      );
      final coordinator = SyncCoordinator(
        syncQueueDao: dao,
        syncRepository: repository,
      );

      await coordinator.drainQueue();

      final pending = await dao.listPending();
      expect(pending, hasLength(1));
      expect(pending.single.syncStatus, 'failed');
    });

    test('concurrent drainQueue calls do not double-submit', () async {
      final dao = database.syncQueueDao;
      await dao.enqueue(
        entityType: 'Transaction',
        entityId: 'txn-1',
        payloadJson: '{}',
      );
      final repository = FakeSyncRepository();
      final coordinator = SyncCoordinator(
        syncQueueDao: dao,
        syncRepository: repository,
      );

      await Future.wait([coordinator.drainQueue(), coordinator.drainQueue()]);

      expect(await dao.listPending(), isEmpty);
    });
  });
}
