import 'package:drift/drift.dart';

import '../app_database.dart';
import '../tables/queued_sale_table.dart';

part 'queued_sale_dao.g.dart';

@DriftAccessor(tables: [QueuedSales])
class QueuedSaleDao extends DatabaseAccessor<AppDatabase>
    with _$QueuedSaleDaoMixin {
  QueuedSaleDao(super.db);

  static const unsyncedStatuses = ['pending', 'syncing'];

  Future<void> enqueue(QueuedSalesCompanion sale) =>
      into(queuedSales).insertOnConflictUpdate(sale);

  /// Sales still to be sent, in receipt-number order — the order they were rung
  /// up. (Not by clock time: receipt numbers are sequential per terminal, so a
  /// device clock adjustment can never reorder them.)
  Future<List<QueuedSale>> listPending(String tenantId, String deviceId) {
    return (select(queuedSales)
          ..where(
            (t) =>
                t.tenantId.equals(tenantId) &
                t.deviceId.equals(deviceId) &
                t.status.equals('pending'),
          )
          ..orderBy([(t) => OrderingTerm.asc(t.receiptNumber)]))
        .get();
  }

  /// Everything a manager may need to look at: unsent sales and rejected ones.
  Future<List<QueuedSale>> listReviewable(String tenantId, String deviceId) {
    return (select(queuedSales)
          ..where(
            (t) =>
                t.tenantId.equals(tenantId) &
                t.deviceId.equals(deviceId) &
                t.status.isIn(['pending', 'syncing', 'rejected']),
          )
          ..orderBy([(t) => OrderingTerm.asc(t.receiptNumber)]))
        .get();
  }

  Stream<List<QueuedSale>> watchReviewable(String tenantId, String deviceId) {
    return (select(queuedSales)
          ..where(
            (t) =>
                t.tenantId.equals(tenantId) &
                t.deviceId.equals(deviceId) &
                t.status.isIn(['pending', 'syncing', 'rejected']),
          )
          ..orderBy([(t) => OrderingTerm.asc(t.receiptNumber)]))
        .watch();
  }

  Future<QueuedSale?> byId(String id) {
    return (select(queuedSales)..where((t) => t.id.equals(id))).getSingleOrNull();
  }

  Future<void> setStatus(
    String id,
    String status, {
    String? lastError,
    bool countAttempt = false,
    bool clearError = false,
  }) async {
    final current = await byId(id);
    if (current == null) {
      return;
    }
    await (update(queuedSales)..where((t) => t.id.equals(id))).write(
      QueuedSalesCompanion(
        status: Value(status),
        attempts: Value(current.attempts + (countAttempt ? 1 : 0)),
        lastError:
            clearError ? const Value(null) : Value(lastError ?? current.lastError),
        syncedAt: status == 'synced' ? Value(DateTime.now()) : const Value.absent(),
      ),
    );
  }

  /// A crash mid-push leaves rows stuck in 'syncing'; they were never
  /// acknowledged, so they simply go back to pending (the idempotency key makes
  /// re-sending safe).
  Future<void> resetStuck(String tenantId, String deviceId) async {
    await (update(queuedSales)..where(
      (t) =>
          t.tenantId.equals(tenantId) &
          t.deviceId.equals(deviceId) &
          t.status.equals('syncing'),
    )).write(const QueuedSalesCompanion(status: Value('pending')));
  }

  /// Confirmed sales are kept briefly as a local audit trail, then dropped.
  Future<void> purgeSyncedBefore(DateTime cutoff) async {
    await (delete(queuedSales)..where(
      (t) => t.status.equals('synced') & t.syncedAt.isSmallerThanValue(cutoff),
    )).go();
  }
}
