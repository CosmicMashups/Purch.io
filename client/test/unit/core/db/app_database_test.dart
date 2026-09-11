import 'package:drift/drift.dart';
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/db/app_database.dart';

void main() {
  group('AppDatabase', () {
    late AppDatabase database;

    setUp(() {
      // In-memory, no real file — proves the schema itself is valid without
      // touching disk. AppDatabase.forTesting exists exactly for this.
      database = AppDatabase.forTesting(NativeDatabase.memory());
    });

    tearDown(() async {
      await database.close();
    });

    test(
      'a queued write survives a read-back — the NFR3 durability path',
      () async {
        await database
            .into(database.pendingSyncQueue)
            .insert(
              PendingSyncQueueCompanion.insert(
                id: 'queue-1',
                entityType: 'Transaction',
                entityId: 'txn-1',
                payloadJson: '{"total":100}',
                createdAt: DateTime.utc(2026, 1, 1),
              ),
            );

        final rows = await database.select(database.pendingSyncQueue).get();

        expect(rows, hasLength(1));
        expect(rows.single.syncStatus, 'pending'); // default value applied
        expect(rows.single.entityType, 'Transaction');
      },
    );

    test(
      'cached branding is keyed by tenant and overwritable on re-sync',
      () async {
        final tenantId = 'tenant-1';
        await database
            .into(database.cachedBranding)
            .insertOnConflictUpdate(
              CachedBrandingCompanion.insert(
                tenantId: tenantId,
                lastSyncedAt: DateTime.utc(2026, 1, 1),
                themeColorHex: const Value('#FF0000'),
              ),
            );

        await database
            .into(database.cachedBranding)
            .insertOnConflictUpdate(
              CachedBrandingCompanion.insert(
                tenantId: tenantId,
                lastSyncedAt: DateTime.utc(2026, 1, 2),
                themeColorHex: const Value('#00FF00'),
              ),
            );

        final rows = await database.select(database.cachedBranding).get();

        expect(rows, hasLength(1)); // updated in place, not duplicated
        expect(rows.single.themeColorHex, '#00FF00');
      },
    );
  });
}
