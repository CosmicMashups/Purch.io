import 'package:drift/drift.dart';

/// The NFR3 durability boundary: every offline-capable write lands here
/// FIRST, before any network call is attempted — so a sale is never lost
/// just because the device (or the LAN link to a Local-mode server) was
/// briefly unreachable. See docs/ARCHITECTURE.md §5 and the implementation
/// plan's "Offline sync scope for v1" note.
///
/// SECURITY — read before using: nothing in the app writes to this table today (offline sales use QueuedSales), but
/// SyncCoordinator is running and sends every row it finds to the server. Rows here are NOT sealed. Before any feature
/// starts writing here, seal them the way QueuedSales does (see SaleQueueIntegrity and QueuedSales.integrity) and have
/// the coordinator refuse rows that fail the check; otherwise anyone with the database file can inject records that the
/// app will send. Details: docs/security/SECURITY-ASSESSMENT.md.
class PendingSyncQueue extends Table {
  TextColumn get id =>
      text()(); // client-generated UUID, doubles as the idempotency key on /sync
  TextColumn get entityType =>
      text()(); // e.g. "Transaction" — which kind of record this is
  TextColumn get entityId =>
      text()(); // the id of that record in its own local table
  TextColumn get payloadJson =>
      text()(); // the full record, serialized, ready to POST as-is
  DateTimeColumn get createdAt => dateTime()();
  TextColumn get syncStatus =>
      text().withDefault(
        const Constant('pending'),
      )(); // pending|syncing|synced|failed

  @override
  Set<Column> get primaryKey => {id};
}
