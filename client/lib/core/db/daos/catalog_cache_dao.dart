import 'package:drift/drift.dart';

import '../app_database.dart';
import '../tables/cached_catalog_list_table.dart';

part 'catalog_cache_dao.g.dart';

@DriftAccessor(tables: [CachedCatalogLists])
class CatalogCacheDao extends DatabaseAccessor<AppDatabase>
    with _$CatalogCacheDaoMixin {
  CatalogCacheDao(super.db);

  Future<CachedCatalogList?> read(String tenantId, String kind) {
    return (select(cachedCatalogLists)
          ..where((t) => t.tenantId.equals(tenantId) & t.kind.equals(kind)))
        .getSingleOrNull();
  }

  Future<void> save({
    required String tenantId,
    required String kind,
    required String? etag,
    required String payloadJson,
    required DateTime validatedAt,
  }) {
    return into(cachedCatalogLists).insertOnConflictUpdate(
      CachedCatalogListsCompanion.insert(
        tenantId: tenantId,
        kind: kind,
        etag: Value(etag),
        payloadJson: payloadJson,
        validatedAt: validatedAt,
      ),
    );
  }

  /// The server said our copy is still current (304): keep the payload, refresh the timestamp.
  Future<void> markValidated(String tenantId, String kind, DateTime at) {
    return (update(cachedCatalogLists)
          ..where((t) => t.tenantId.equals(tenantId) & t.kind.equals(kind)))
        .write(CachedCatalogListsCompanion(validatedAt: Value(at)));
  }

  Future<void> clearTenant(String tenantId) {
    return (delete(cachedCatalogLists)
          ..where((t) => t.tenantId.equals(tenantId)))
        .go();
  }
}
