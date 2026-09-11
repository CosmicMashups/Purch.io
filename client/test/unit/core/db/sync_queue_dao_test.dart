import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/db/app_database.dart';

void main() {
  group('SyncQueueDao', () {
    late AppDatabase database;

    setUp(() {
      database = AppDatabase.forTesting(NativeDatabase.memory());
    });

    tearDown(() async {
      await database.close();
    });

    test('enqueue generates a unique id and defaults to pending', () async {
      final dao = database.syncQueueDao;

      final firstId = await dao.enqueue(
        entityType: 'Transaction',
        entityId: 'txn-1',
        payloadJson: '{}',
      );
      final secondId = await dao.enqueue(
        entityType: 'Transaction',
        entityId: 'txn-2',
        payloadJson: '{}',
      );

      expect(firstId, isNot(secondId));

      final pending = await dao.listPending();
      expect(pending, hasLength(2));
      expect(pending.every((row) => row.syncStatus == 'pending'), isTrue);
    });

    test('listPending includes failed rows but not synced ones', () async {
      final dao = database.syncQueueDao;
      final pendingId = await dao.enqueue(
        entityType: 'Transaction',
        entityId: 'txn-1',
        payloadJson: '{}',
      );
      final failedId = await dao.enqueue(
        entityType: 'Transaction',
        entityId: 'txn-2',
        payloadJson: '{}',
      );
      final syncedId = await dao.enqueue(
        entityType: 'Transaction',
        entityId: 'txn-3',
        payloadJson: '{}',
      );

      await dao.markFailed([failedId]);
      await dao.markSynced([syncedId]);

      final pending = await dao.listPending();

      expect(pending.map((row) => row.id), containsAll([pendingId, failedId]));
      expect(pending.map((row) => row.id), isNot(contains(syncedId)));
    });

    test('markSyncing/markSynced/markFailed update status in place', () async {
      final dao = database.syncQueueDao;
      final id = await dao.enqueue(
        entityType: 'Transaction',
        entityId: 'txn-1',
        payloadJson: '{}',
      );

      Future<PendingSyncQueueData> readRow() async {
        final rows = await database.select(database.pendingSyncQueue).get();
        return rows.singleWhere((r) => r.id == id);
      }

      await dao.markSyncing([id]);
      expect((await readRow()).syncStatus, 'syncing');

      await dao.markFailed([id]);
      expect((await readRow()).syncStatus, 'failed');
      expect((await dao.listPending()).map((r) => r.id), contains(id));

      await dao.markSynced([id]);
      expect((await readRow()).syncStatus, 'synced');
      expect((await dao.listPending()), isEmpty);
    });

    test('marking an empty id list is a safe no-op', () async {
      final dao = database.syncQueueDao;
      await dao.enqueue(
        entityType: 'Transaction',
        entityId: 'txn-1',
        payloadJson: '{}',
      );

      await dao.markSynced([]);

      expect(await dao.listPending(), hasLength(1));
    });
  });
}
