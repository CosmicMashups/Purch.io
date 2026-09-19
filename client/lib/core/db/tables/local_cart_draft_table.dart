import 'package:drift/drift.dart';

/// The Cashier's in-progress cart, persisted after every change so a crash,
/// restart or app update never loses a sale being rung up — and so nothing has
/// to touch the server until checkout. One row per (tenant, device); keying on
/// the tenant means a draft can never surface under another tenant's session.
class LocalCartDrafts extends Table {
  TextColumn get draftKey => text()();
  TextColumn get json => text()();
  DateTimeColumn get updatedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {draftKey};
}
