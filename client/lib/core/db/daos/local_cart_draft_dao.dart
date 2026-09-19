import 'package:drift/drift.dart';

import '../app_database.dart';
import '../tables/local_cart_draft_table.dart';

part 'local_cart_draft_dao.g.dart';

@DriftAccessor(tables: [LocalCartDrafts])
class LocalCartDraftDao extends DatabaseAccessor<AppDatabase>
    with _$LocalCartDraftDaoMixin {
  LocalCartDraftDao(super.db);

  Future<String?> read(String key) async {
    final row =
        await (select(localCartDrafts)
          ..where((t) => t.draftKey.equals(key))).getSingleOrNull();
    return row?.json;
  }

  Future<void> write(String key, String json) {
    return into(localCartDrafts).insertOnConflictUpdate(
      LocalCartDraftsCompanion.insert(
        draftKey: key,
        json: json,
        updatedAt: DateTime.now(),
      ),
    );
  }

  Future<void> clear(String key) async {
    await (delete(localCartDrafts)..where((t) => t.draftKey.equals(key))).go();
  }
}
