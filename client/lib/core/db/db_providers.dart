import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import 'app_database.dart';
import 'daos/device_identity_dao.dart';
import 'daos/local_cart_draft_dao.dart';
import 'daos/queued_sale_dao.dart';
import 'daos/sync_queue_dao.dart';

part 'db_providers.g.dart';

/// Split out from sync_providers.dart so both it and auth_providers.dart can
/// depend on the database without depending on each other.
@Riverpod(keepAlive: true)
AppDatabase appDatabase(Ref ref) {
  final db = AppDatabase();
  ref.onDispose(db.close);
  return db;
}

@Riverpod(keepAlive: true)
SyncQueueDao syncQueueDao(Ref ref) {
  return ref.watch(appDatabaseProvider).syncQueueDao;
}

@Riverpod(keepAlive: true)
DeviceIdentityDao deviceIdentityDao(Ref ref) {
  return ref.watch(appDatabaseProvider).deviceIdentityDao;
}

@Riverpod(keepAlive: true)
LocalCartDraftDao localCartDraftDao(Ref ref) {
  return ref.watch(appDatabaseProvider).localCartDraftDao;
}

@Riverpod(keepAlive: true)
QueuedSaleDao queuedSaleDao(Ref ref) {
  return ref.watch(appDatabaseProvider).queuedSaleDao;
}
