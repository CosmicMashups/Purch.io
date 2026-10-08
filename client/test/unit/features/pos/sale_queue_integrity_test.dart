import 'package:drift/drift.dart' show Value;
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/db/app_database.dart';
import 'package:purch_client/features/pos/data/drift_sale_queue_store.dart';
import 'package:purch_client/features/pos/data/sale_queue.dart';
import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';

import '../../../helpers/fake_sale_queue_integrity.dart';

SaleQueueEntry _entry(String id, int number, {double total = 100}) => SaleQueueEntry(
  saleId: id,
  receiptNumber: number,
  totalAmount: total,
  soldAt: DateTime(2026, 10, 8, 10),
  request: CheckoutRequest(
    saleId: id,
    lines: const [AddTransactionLineRequest(itemId: 'coffee', quantity: 1)],
    seniorPwdDiscountApplied: false,
    promoCode: null,
    orderType: null,
    payment: const RecordPaymentRequest(method: PaymentMethod.cash, amountTendered: 200),
    expectedTotal: total,
    receiptNumber: number,
    offlineSale: true,
  ),
);

void main() {
  late AppDatabase database;
  late InMemorySealStore seals;

  DriftSaleQueueStore storeFor() => DriftSaleQueueStore(
    dao: database.queuedSaleDao,
    identityDao: database.deviceIdentityDao,
    integrity: seals.build(),
  );

  Future<void> signInAs(String tenant, {String device = 'd1'}) =>
      database.deviceIdentityDao.saveIdentity(deviceId: device, tenantId: tenant, branchId: 'b');

  setUp(() {
    database = AppDatabase.forTesting(NativeDatabase.memory());
    seals = InMemorySealStore();
  });
  tearDown(() => database.close());

  group('tamper check on queued sales', () {
    test('an untouched sale is sent, and moving through statuses does not break its seal', () async {
      await signInAs('t1');
      final store = storeFor();
      await store.enqueue(_entry('s1', 1));

      expect((await store.pending()).map((e) => e.saleId), ['s1']);

      await store.markSyncing('s1');
      await store.markPending('s1', error: 'no route');
      expect((await store.pending()).map((e) => e.saleId), ['s1']);
    });

    test('a sale whose total was lowered on disk is not sent and goes to review with the reason', () async {
      await signInAs('t1');
      final store = storeFor();
      await store.enqueue(_entry('s1', 1, total: 500));

      await (database.update(database.queuedSales)..where((t) => t.id.equals('s1')))
          .write(const QueuedSalesCompanion(totalAmount: Value(5)));

      expect(await store.pending(), isEmpty);
      final review = await store.reviewable();
      expect(review.single.status, SaleQueueStatus.rejected);
      expect(review.single.lastError, DriftSaleQueueStore.tamperedMessage);
    });

    test('a changed request (a discount switched on, a payment swapped) is not sent', () async {
      await signInAs('t1');
      final store = storeFor();
      await store.enqueue(_entry('s1', 1));
      final row = (await database.queuedSaleDao.byId('s1'))!;

      await (database.update(database.queuedSales)..where((t) => t.id.equals('s1')))
          .write(QueuedSalesCompanion(requestJson: Value(row.requestJson.replaceFirst('"seniorPwdDiscountApplied":false', '"seniorPwdDiscountApplied":true'))));

      expect(await store.pending(), isEmpty);
    });

    test('clearing the seal of an edited row does not get it past the check', () async {
      await signInAs('t1');
      final store = storeFor();
      await store.enqueue(_entry('s1', 1, total: 500));
      expect((await store.pending()), hasLength(1)); // first use: legacy rows sealed, flag set

      await (database.update(database.queuedSales)..where((t) => t.id.equals('s1')))
          .write(const QueuedSalesCompanion(totalAmount: Value(5), integrity: Value(null)));

      expect(await storeFor().pending(), isEmpty);
    });

    test('sales queued before sealing existed are sealed once, then sent', () async {
      await signInAs('t1');
      // A row as the previous app version wrote it: no seal.
      await database.queuedSaleDao.enqueue(
        QueuedSalesCompanion.insert(
          id: 'old',
          tenantId: 't1',
          deviceId: 'd1',
          receiptNumber: 7,
          totalAmount: 100,
          requestJson: _entry('old', 7).requestJson,
          soldAt: DateTime(2026, 10, 8, 9),
        ),
      );

      final store = storeFor();
      expect((await store.pending()).map((e) => e.saleId), ['old']);
      expect(seals.sealed, isTrue);
      expect((await database.queuedSaleDao.byId('old'))!.integrity, isNotNull);
    });

    test('a different key, such as a reinstall, cannot validate old seals', () async {
      await signInAs('t1');
      await storeFor().enqueue(_entry('s1', 1));

      seals.key = null; // the keystore was wiped
      expect(await storeFor().pending(), isEmpty);
    });
  });

  group('wiping what a terminal cached', () {
    Future<void> fillCaches() async {
      await database.localCartDraftDao.write('t1:d1', '{"lines":[1]}');
      await database.catalogCacheDao.save(tenantId: 't1', kind: 'items', etag: 'e', payloadJson: '[]', validatedAt: DateTime(2026, 10, 8));
      final store = storeFor();
      await store.enqueue(_entry('unsent', 1));
      await store.enqueue(_entry('done', 2));
      await store.markSynced('done');
      await store.enqueue(_entry('dismissed', 3));
      await store.dismiss('dismissed');
    }

    test('moving the app to a different business clears the old business cache but never an unsent sale', () async {
      await signInAs('t1');
      await fillCaches();

      await signInAs('t2', device: 'd2');

      expect(await database.localCartDraftDao.read('t1:d1'), isNull);
      expect(await database.catalogCacheDao.read('t1', 'items'), isNull);
      expect(await database.queuedSaleDao.byId('done'), isNull);
      expect(await database.queuedSaleDao.byId('dismissed'), isNull);
      expect(await database.queuedSaleDao.byId('unsent'), isNotNull);
    });

    test('signing in again as the same terminal keeps its cache', () async {
      await signInAs('t1');
      await fillCaches();

      await signInAs('t1');

      expect(await database.localCartDraftDao.read('t1:d1'), isNotNull);
      expect(await database.catalogCacheDao.read('t1', 'items'), isNotNull);
    });

    test('unpairing wipes the caches, keeps the identity and every sale the server has not acknowledged', () async {
      await signInAs('t1');
      await fillCaches();
      await database.queuedSaleDao.setStatus('unsent', 'rejected', lastError: 'x');

      await database.deviceIdentityDao.wipeCachedData();

      expect(await database.localCartDraftDao.read('t1:d1'), isNull);
      expect(await database.catalogCacheDao.read('t1', 'items'), isNull);
      expect(await database.queuedSaleDao.byId('unsent'), isNotNull);
      expect(await database.queuedSaleDao.byId('done'), isNull);
      expect(await database.deviceIdentityDao.getIdentity(), isNotNull);
    });
  });
}
