import 'package:drift/drift.dart';

/// Read-through cache of the logged-in tenant's branding, fetched on
/// login/sync and used by core/theming/theme_builder.dart to build a runtime
/// ThemeData — never a hardcoded brand asset. Single-row-per-device in
/// practice, since a device only ever serves one tenant.
class CachedBranding extends Table {
  TextColumn get tenantId => text()();
  TextColumn get logoUrl => text().nullable()();
  TextColumn get themeColorHex => text().nullable()();
  TextColumn get fontFamily => text().nullable()();
  DateTimeColumn get lastSyncedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {tenantId};
}
