import 'dart:convert';


import '../../../core/db/app_database.dart';
import '../../../core/db/daos/device_identity_dao.dart';
import '../../../core/db/daos/queued_sale_dao.dart';
import '../domain/transaction_models.dart';
import 'sale_queue.dart';

/// Keeps queued offline sales in the app's local SQLite database, scoped to the
/// signed-in tenant and this terminal.
class DriftSaleQueueStore implements SaleQueueStore {
  DriftSaleQueueStore({
    required QueuedSaleDao dao,
    required DeviceIdentityDao identityDao,
  }) : _dao = dao,
       _identityDao = identityDao;

  final QueuedSaleDao _dao;
  final DeviceIdentityDao _identityDao;

  Future<DeviceIdentityData> _identity() async {
    final identity = await _identityDao.getIdentity();
    if (identity == null) {
      // Without a tenant and terminal there is nothing to attribute the sale
      // to, and it could later be pushed under the wrong account.
      throw StateError('This terminal has no identity yet; cannot queue a sale.');
    }
    return identity;
  }

  @override
  Future<void> enqueue(SaleQueueEntry entry) async {
    final identity = await _identity();
    await _dao.enqueue(
      QueuedSalesCompanion.insert(
        id: entry.saleId,
        tenantId: identity.tenantId,
        deviceId: identity.deviceId,
        receiptNumber: entry.receiptNumber,
        totalAmount: entry.totalAmount,
        requestJson: entry.requestJson,
        soldAt: entry.soldAt,
      ),
    );
  }

  @override
  Future<List<SaleQueueEntry>> pending() async {
    final identity = await _identity();
    return (await _dao.listPending(
      identity.tenantId,
      identity.deviceId,
    )).map(_toEntry).toList();
  }

  @override
  Future<List<SaleQueueEntry>> reviewable() async {
    final identity = await _identity();
    return (await _dao.listReviewable(
      identity.tenantId,
      identity.deviceId,
    )).map(_toEntry).toList();
  }

  @override
  Stream<List<SaleQueueEntry>> watchReviewable() async* {
    final identity = await _identityDao.getIdentity();
    if (identity == null) {
      yield const [];
      return;
    }
    yield* _dao
        .watchReviewable(identity.tenantId, identity.deviceId)
        .map((rows) => rows.map(_toEntry).toList());
  }

  @override
  Future<void> markSyncing(String saleId) => _dao.setStatus(saleId, 'syncing');

  @override
  Future<void> markPending(String saleId, {String? error}) => _dao.setStatus(
    saleId,
    'pending',
    lastError: error,
    countAttempt: true,
  );

  @override
  Future<void> markSynced(String saleId) =>
      _dao.setStatus(saleId, 'synced', clearError: true);

  @override
  Future<void> markRejected(String saleId, String reason) =>
      _dao.setStatus(saleId, 'rejected', lastError: reason, countAttempt: true);

  @override
  Future<void> dismiss(String saleId) => _dao.setStatus(saleId, 'dismissed');

  @override
  Future<void> resetStuck() async {
    final identity = await _identity();
    await _dao.resetStuck(identity.tenantId, identity.deviceId);
  }

  @override
  Future<void> purgeOldSynced(Duration keepFor) =>
      _dao.purgeSyncedBefore(DateTime.now().subtract(keepFor));

  SaleQueueEntry _toEntry(QueuedSale row) {
    return SaleQueueEntry(
      saleId: row.id,
      receiptNumber: row.receiptNumber,
      totalAmount: row.totalAmount,
      soldAt: row.soldAt,
      request: CheckoutRequest.fromJson(
        jsonDecode(row.requestJson) as Map<String, dynamic>,
      ),
      status: SaleQueueStatus.values.firstWhere(
        (status) => status.name == row.status,
        orElse: () => SaleQueueStatus.pending,
      ),
      attempts: row.attempts,
      lastError: row.lastError,
    );
  }
}
