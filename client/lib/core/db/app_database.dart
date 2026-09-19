import 'dart:io';

import 'package:drift/drift.dart';
import 'package:drift/native.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import 'daos/device_identity_dao.dart';
import 'daos/local_cart_draft_dao.dart';
import 'daos/queued_sale_dao.dart';
import 'daos/sync_queue_dao.dart';
import 'tables/cached_branding_table.dart';
import 'tables/device_identity_table.dart';
import 'tables/local_cart_draft_table.dart';
import 'tables/pending_sync_queue_table.dart';
import 'tables/queued_sale_table.dart';

part 'app_database.g.dart';

/// The one local SQLite database backing the app's offline cache and write
/// queue. Deliberately a small, hand-picked subset of the backend's schema —
/// only what a device needs cached/queued locally, not a full mirror — see
/// docs/ARCHITECTURE.md §2 ("Local persistence").
@DriftDatabase(
  tables: [
    PendingSyncQueue,
    CachedBranding,
    DeviceIdentity,
    LocalCartDrafts,
    QueuedSales,
  ],
  daos: [SyncQueueDao, DeviceIdentityDao, LocalCartDraftDao, QueuedSaleDao],
)
class AppDatabase extends _$AppDatabase {
  AppDatabase() : super(_openConnection());

  AppDatabase.forTesting(super.executor) : super();

  @override
  int get schemaVersion => 6;

  @override
  MigrationStrategy get migration => MigrationStrategy(
    onUpgrade: (m, from, to) async {
      if (from < 2) {
        // v1 → v2: add kioskPosterImageUrl to cached_branding.
        await m.addColumn(
          cachedBranding,
          cachedBranding.kioskPosterImageUrl,
        );
      }
      if (from < 3) {
        // v2 → v3: the single themeColorHex became four colour columns.
        // cached_branding is a read-through cache re-fetched on login/sync,
        // so dropping and recreating it loses nothing durable.
        await m.deleteTable(cachedBranding.actualTableName);
        await m.createTable(cachedBranding);
      }
      if (from < 4) {
        // v3 → v4: new device_identity table — this device's own id/tenant/
        // branch plus its cached last-issued receipt number.
        await m.createTable(deviceIdentity);
      }
      if (from < 5) {
        // v4 → v5: the Cashier's on-device draft cart.
        await m.createTable(localCartDrafts);
      }
      if (from < 6) {
        // v5 → v6: sales completed offline, waiting to be sent to the server.
        await m.createTable(queuedSales);
      }
    },
  );
}

LazyDatabase _openConnection() {
  return LazyDatabase(() async {
    final directory = await getApplicationDocumentsDirectory();
    final file = File(p.join(directory.path, 'purch.sqlite'));
    return NativeDatabase.createInBackground(file);
  });
}
