import 'package:drift/drift.dart';

/// Sales that were completed at the counter while the terminal could not reach
/// the server: the receipt was already handed over, the money already taken,
/// and the sale now waits here until it can be sent through checkout. The row
/// is the durable record of that sale until the server confirms it, so it is
/// written before the receipt prints and never deleted before the server has
/// acknowledged it.
///
/// [id] is the sale's idempotency key (the checkout `saleId`): however many
/// times a push is retried, the server records the sale once.
class QueuedSales extends Table {
  TextColumn get id => text()();

  /// Scoped to tenant + terminal, so one tenant's queued sales can never be
  /// pushed, counted or shown under another tenant's session on a shared device.
  TextColumn get tenantId => text()();
  TextColumn get deviceId => text()();

  IntColumn get receiptNumber => integer()();
  RealColumn get totalAmount => real()();

  /// The whole checkout request (lines, discounts, payment, receipt number,
  /// offline flag, original sale time), ready to send as-is.
  TextColumn get requestJson => text()();

  /// When the sale really happened at the counter.
  DateTimeColumn get soldAt => dateTime()();

  /// pending | syncing | synced | rejected | dismissed
  TextColumn get status => text().withDefault(const Constant('pending'))();
  IntColumn get attempts => integer().withDefault(const Constant(0))();
  TextColumn get lastError => text().nullable()();
  DateTimeColumn get syncedAt => dateTime().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}
