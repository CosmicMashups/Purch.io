import 'dart:io';

import 'package:drift/drift.dart';
import 'package:drift/native.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import 'daos/sync_queue_dao.dart';
import 'tables/cached_branding_table.dart';
import 'tables/pending_sync_queue_table.dart';

part 'app_database.g.dart';

/// The one local SQLite database backing the app's offline cache and write
/// queue. Deliberately a small, hand-picked subset of the backend's schema —
/// only what a device needs cached/queued locally, not a full mirror — see
/// docs/ARCHITECTURE.md §2 ("Local persistence").
@DriftDatabase(tables: [PendingSyncQueue, CachedBranding], daos: [SyncQueueDao])
class AppDatabase extends _$AppDatabase {
  AppDatabase() : super(_openConnection());

  AppDatabase.forTesting(super.executor) : super();

  @override
  int get schemaVersion => 1;
}

LazyDatabase _openConnection() {
  return LazyDatabase(() async {
    final directory = await getApplicationDocumentsDirectory();
    final file = File(p.join(directory.path, 'purch.sqlite'));
    return NativeDatabase.createInBackground(file);
  });
}
