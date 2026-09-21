import 'package:drift/drift.dart';

/// The last good copy of a catalog list (items, categories) as the server sent it, plus the ETag it
/// came with. Lets the app revalidate cheaply (If-None-Match -> 304) and lets a terminal that
/// cold-starts offline still show a catalog. One row per (tenant, list); the tenant key means a
/// device re-paired to another business can never read the previous business's catalog.
class CachedCatalogLists extends Table {
  TextColumn get tenantId => text()();

  /// 'items' or 'categories'.
  TextColumn get kind => text()();

  TextColumn get etag => text().nullable()();

  /// The response body exactly as received (a JSON array).
  TextColumn get payloadJson => text()();

  /// When the server last confirmed this copy was current (a 200 or a 304), not merely when it was written.
  DateTimeColumn get validatedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {tenantId, kind};
}
