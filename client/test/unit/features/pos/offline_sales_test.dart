import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/db/app_database.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/pos/data/drift_sale_queue_store.dart';
import 'package:purch_client/features/pos/data/local_first_pos_repository.dart';
import 'package:purch_client/features/pos/data/sale_queue.dart';
import 'package:purch_client/features/pos/data/sale_sync_coordinator.dart';
import 'package:purch_client/features/pos/domain/offline_limits.dart';
import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/pos_repository.dart';
import 'package:purch_client/features/pos/domain/pricing_engine.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';

import '../../../helpers/fake_catalog_repository.dart';

Item _coffee() => const Item(
  id: 'coffee',
  name: 'Coffee',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 100,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 10,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

/// A server that can be made unreachable, and that remembers sales by sale id
/// like the real idempotent checkout.
class _Server implements PosRepository {
  Object? failure;
  final requests = <CheckoutRequest>[];
  final recorded = <String, Transaction>{};

  Transaction _sale(CheckoutRequest r) => Transaction(
    id: 'srv-${r.saleId}',
    branchId: 'b',
    deviceId: 'd',
    status: TransactionStatus.completed,
    lines: const [],
    subtotal: 0,
    discountAmount: 0,
    seniorPwdDiscountApplied: false,
    promoCode: null,
    promoDiscountAmount: 0,
    totalAmount: r.expectedTotal ?? 0,
    receiptNumber: r.receiptNumber,
    payments: const [],
  );

  @override
  Future<Transaction> checkout(CheckoutRequest request) async {
    requests.add(request);
    if (failure != null) {
      throw failure!;
    }
    return recorded.putIfAbsent(request.saleId, () => _sale(request));
  }

  @override
  Future<int> getLastIssuedReceiptNumber() async => 0;

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

SaleQueueEntry _entry(
  String id, {
  int number = 1,
  DateTime? soldAt,
  SaleQueueStatus status = SaleQueueStatus.pending,
  int attempts = 0,
}) => SaleQueueEntry(
  saleId: id,
  receiptNumber: number,
  totalAmount: 100,
  soldAt: soldAt ?? DateTime(2026, 9, 20, 10),
  status: status,
  attempts: attempts,
  request: CheckoutRequest(
    saleId: id,
    lines: const [AddTransactionLineRequest(itemId: 'coffee', quantity: 1)],
    seniorPwdDiscountApplied: false,
    promoCode: null,
    orderType: null,
    payment: const RecordPaymentRequest(
      method: PaymentMethod.cash,
      amountTendered: 100,
    ),
    expectedTotal: 100,
    receiptNumber: number,
    offlineSale: true,
    soldAt: soldAt ?? DateTime(2026, 9, 20, 10),
  ),
);

void main() {
  group('offline limits', () {
    const limits = OfflineLimits();
    final now = DateTime(2026, 9, 20, 12);

    OfflineLevel level(int count, {Duration? age}) => limits.evaluate(
      unsyncedCount: count,
      oldestUnsyncedAt: age == null ? null : now.subtract(age),
      now: now,
    );

    test('nothing waiting is fine', () => expect(level(0), OfflineLevel.ok));

    test('warns at 80% of the sale count and blocks at 100%', () {
      expect(level(159), OfflineLevel.ok);
      expect(level(160), OfflineLevel.warning);
      expect(level(199), OfflineLevel.warning);
      expect(level(200), OfflineLevel.blocked);
    });

    test('warns at 80% of the age limit and blocks at 24 hours', () {
      expect(level(1, age: const Duration(hours: 19)), OfflineLevel.ok);
      expect(level(1, age: const Duration(hours: 20)), OfflineLevel.warning);
      expect(level(1, age: const Duration(hours: 24)), OfflineLevel.blocked);
    });

    test('whichever limit is closer decides', () {
      expect(level(10, age: const Duration(hours: 25)), OfflineLevel.blocked);
      expect(level(200, age: const Duration(minutes: 5)), OfflineLevel.blocked);
    });

    test('stats count only unsent sales toward the limits, and rejected ones separately', () {
      final stats = SaleQueueStats.of([
        _entry('a', soldAt: DateTime(2026, 9, 20, 8)),
        _entry('b', status: SaleQueueStatus.syncing),
        _entry('c', status: SaleQueueStatus.rejected),
      ]);
      expect(stats.unsynced, 2);
      expect(stats.rejected, 1);
      expect(stats.oldestUnsyncedAt, DateTime(2026, 9, 20, 8));
    });
  });

  group('completing a sale offline', () {
    late _Server server;
    late MemoryCartDraftStore drafts;
    late MemorySaleQueueStore queue;
    late bool connected;
    late DateTime clock;
    late int localCounter;
    late List<int> retired;

    LocalFirstPosRepository build({SaleQueueStore? saleQueue}) =>
        LocalFirstPosRepository(
          drainQueue:
              () => SaleSyncCoordinator(
                store: queue,
                remote: server,
                onSynced: () {},
              ).drain(),
          remote: server,
          catalog: FakeCatalogRepository(),
          loadItems: () async => [_coffee()],
          loadRules: () async => PricingRules.empty,
          store: drafts,
          identity:
              () async => const CartIdentity(
                tenantId: 't',
                deviceId: 'd',
                branchId: 'b',
              ),
          lastIssuedReceiptNumber: ({bool refresh = false}) async => localCounter,
          recordReceiptNumber: (n) async {
            retired.add(n);
            if (n > localCounter) {
              localCounter = n;
            }
          },
          saleQueue: saleQueue ?? queue,
          isConnected: () async => connected,
          clock: () => clock,
        );

    const cash = RecordPaymentRequest(
      method: PaymentMethod.cash,
      amountTendered: 500,
    );

    Future<LocalFirstPosRepository> withCoffee({int qty = 2}) async {
      final repo = build();
      await repo.addLine(AddTransactionLineRequest(itemId: 'coffee', quantity: qty.toDouble()));
      return repo;
    }

    setUp(() {
      server = _Server();
      drafts = MemoryCartDraftStore();
      queue = MemorySaleQueueStore();
      connected = true;
      clock = DateTime(2026, 9, 20, 10);
      localCounter = 0;
      retired = [];
    });

    test('a sale that cannot reach the server is completed on the device and queued', () async {
      server.failure = const NetworkFailure('no route');
      final repo = await withCoffee();

      final sale = await repo.recordPayment(cash);

      expect(sale.savedOffline, isTrue);
      expect(sale.status, TransactionStatus.completed);
      expect(sale.receiptNumber, 1);
      expect(sale.totalAmount, 200);
      expect(sale.payments.single.changeGiven, 300);
      expect(sale.lines.single.itemName, 'Coffee');

      final queued = queue.entries.values.single;
      expect(queued.status, SaleQueueStatus.pending);
      expect(queued.receiptNumber, 1);
      expect(queued.request.offlineSale, isTrue);
      expect(queued.request.soldAt, clock);
      expect(queued.request.receiptNumber, 1);
      expect(queued.request.lines.single.quantity, 2);

      expect(retired, [1], reason: 'the number is now issued, never reused');
      expect((await repo.getOrCreateOpenCart()).lines, isEmpty);
    });

    test('the queued sale reuses the sale id, so the server cannot record it twice', () async {
      server.failure = const NetworkFailure('no route');
      final repo = await withCoffee();
      await repo.recordPayment(cash);

      // The failed online attempt and the queued sale carry the same id.
      expect(server.requests.single.saleId, queue.entries.values.single.saleId);
    });

    test('a terminal that is plainly offline never waits on the network', () async {
      connected = false;
      final repo = await withCoffee();

      final sale = await repo.recordPayment(cash);

      expect(sale.savedOffline, isTrue);
      expect(server.requests, isEmpty);
    });

    test('after one failed attempt the next sales skip the network for a while', () async {
      server.failure = const NetworkFailure('no route');
      final repo = await withCoffee();
      await repo.recordPayment(cash);
      expect(server.requests, hasLength(1));

      await repo.addLine(const AddTransactionLineRequest(itemId: 'coffee', quantity: 1));
      clock = clock.add(const Duration(seconds: 10));
      final second = await repo.recordPayment(cash);

      expect(second.savedOffline, isTrue);
      expect(second.receiptNumber, 2);
      expect(server.requests, hasLength(1), reason: 'no second timeout for the customer to wait out');

      // ...but once the backoff has passed, the server is tried again.
      server.failure = null;
      await repo.addLine(const AddTransactionLineRequest(itemId: 'coffee', quantity: 1));
      clock = clock.add(const Duration(minutes: 5));
      final third = await repo.recordPayment(cash);
      expect(third.savedOffline, isFalse);
      // The failed first attempt, then the two waiting sales drained (in order,
      // before the new one is allowed to overtake them), then the new sale itself.
      expect(server.requests.map((r) => r.receiptNumber), [1, 1, 2, 3]);
    });

    test('a definitive server refusal is not turned into an offline sale', () async {
      server.failure = const ConflictFailure('Prices or promos changed: 5.00');
      final repo = await withCoffee();

      await expectLater(repo.recordPayment(cash), throwsA(isA<ConflictFailure>()));

      expect(queue.entries, isEmpty);
      expect(retired, isEmpty);
    });

    test('credit (utang) sales cannot be completed offline', () async {
      server.failure = const NetworkFailure('no route');
      final repo = await withCoffee();

      await expectLater(
        repo.recordPayment(
          const RecordPaymentRequest(
            method: PaymentMethod.utangCredit,
            customerCreditLedgerId: 'ledger-1',
          ),
        ),
        throwsA(isA<NetworkFailure>()),
      );

      expect(queue.entries, isEmpty);
    });

    test('bank transfer and manual GCash QR can be completed offline', () async {
      server.failure = const NetworkFailure('no route');
      final repo = await withCoffee();
      final sale = await repo.recordPayment(
        const RecordPaymentRequest(method: PaymentMethod.manualGcashQr),
      );
      expect(sale.savedOffline, isTrue);
      expect(sale.payments.single.changeGiven, isNull);
    });

    test('cash that does not cover the total is refused offline just like online', () async {
      connected = false;
      final repo = await withCoffee();

      await expectLater(
        repo.recordPayment(
          const RecordPaymentRequest(method: PaymentMethod.cash, amountTendered: 50),
        ),
        throwsA(isA<ValidationFailure>()),
      );

      expect(queue.entries, isEmpty);
      expect(retired, isEmpty);
      expect((await repo.getOrCreateOpenCart()).lines, hasLength(1));

      // The reserved number was never sent, so it is still available.
      final sale = await repo.recordPayment(cash);
      expect(sale.receiptNumber, 1);
    });

    test('at the offline limit no further sale can be completed, and the cart is kept', () async {
      connected = false;
      for (var i = 0; i < 200; i++) {
        await queue.enqueue(_entry('sale-$i', number: i + 1));
      }
      localCounter = 200;
      final repo = await withCoffee();

      await expectLater(
        repo.recordPayment(cash),
        throwsA(
          isA<NetworkFailure>().having((f) => f.message, 'message', contains('offline selling limit')),
        ),
      );

      expect(queue.entries, hasLength(200));
      expect((await repo.getOrCreateOpenCart()).lines, hasLength(1));
      expect(retired, isEmpty);
    });

    test('a sale left waiting longer than 24 hours blocks further offline sales', () async {
      connected = false;
      await queue.enqueue(_entry('old', soldAt: clock.subtract(const Duration(hours: 25))));
      final repo = await withCoffee();

      await expectLater(repo.recordPayment(cash), throwsA(isA<NetworkFailure>()));
    });

    test('without a queue configured, a failed payment simply fails', () async {
      server.failure = const NetworkFailure('no route');
      final repo = LocalFirstPosRepository(
        remote: server,
        catalog: FakeCatalogRepository(),
        loadItems: () async => [_coffee()],
        loadRules: () async => PricingRules.empty,
        store: drafts,
        identity: () async => null,
        lastIssuedReceiptNumber: ({bool refresh = false}) async => 0,
        recordReceiptNumber: (_) async {},
      );
      await repo.addLine(const AddTransactionLineRequest(itemId: 'coffee', quantity: 1));

      await expectLater(repo.recordPayment(cash), throwsA(isA<NetworkFailure>()));
    });
  });

  group('SaleSyncCoordinator', () {
    late _Server server;
    late MemorySaleQueueStore queue;
    late int syncedCallbacks;
    late SaleSyncCoordinator coordinator;

    setUp(() {
      server = _Server();
      queue = MemorySaleQueueStore();
      syncedCallbacks = 0;
      coordinator = SaleSyncCoordinator(
        store: queue,
        remote: server,
        onSynced: () => syncedCallbacks++,
      );
    });

    test('sends waiting sales oldest first and marks them synced', () async {
      await queue.enqueue(_entry('later', number: 2, soldAt: DateTime(2026, 9, 20, 11)));
      await queue.enqueue(_entry('earlier', number: 1, soldAt: DateTime(2026, 9, 20, 9)));

      await coordinator.drain();

      expect(server.requests.map((r) => r.saleId), ['earlier', 'later']);
      expect(queue['earlier']!.status, SaleQueueStatus.synced);
      expect(queue['later']!.status, SaleQueueStatus.synced);
      expect(syncedCallbacks, 1, reason: 'dashboards refresh once, after the batch');
      expect(server.requests.every((r) => r.offlineSale), isTrue);
    });

    test('an unreachable server leaves every sale queued and stops early', () async {
      server.failure = const NetworkFailure('offline');
      await queue.enqueue(_entry('a', soldAt: DateTime(2026, 9, 20, 9)));
      await queue.enqueue(_entry('b', number: 2, soldAt: DateTime(2026, 9, 20, 10)));

      await coordinator.drain();

      expect(queue['a']!.status, SaleQueueStatus.pending);
      expect(queue['a']!.attempts, 1);
      expect(queue['b']!.attempts, 0, reason: 'not attempted once the first failed');
      expect(server.requests, hasLength(1));
      expect(syncedCallbacks, 0);
    });

    test('an expired session keeps the sales safe until someone signs in again', () async {
      server.failure = const UnauthorizedFailure('session expired');
      await queue.enqueue(_entry('a'));

      await coordinator.drain();

      expect(queue['a']!.status, SaleQueueStatus.pending);
    });

    test('a sale the server refuses is set aside for review and does not block the next one', () async {
      await queue.enqueue(_entry('bad', soldAt: DateTime(2026, 9, 20, 9)));
      await queue.enqueue(_entry('good', number: 2, soldAt: DateTime(2026, 9, 20, 10)));
      server.failure = const ValidationFailure('This item is not active.', {});

      // The first sale is refused; then the server recovers for the second.
      await coordinator.drain();
      server.failure = null;
      await queue.markPending('good');
      await coordinator.drain();

      expect(queue['bad']!.status, SaleQueueStatus.rejected);
      expect(queue['bad']!.lastError, contains('not active'));
      expect(queue['good']!.status, SaleQueueStatus.synced);
    });

    test('a repeated server error eventually sets the sale aside instead of blocking the queue', () async {
      server.failure = const UnknownFailure('boom');
      await queue.enqueue(
        _entry('stuck', attempts: SaleSyncCoordinator.maxServerErrorAttempts),
      );

      await coordinator.drain();

      expect(queue['stuck']!.status, SaleQueueStatus.rejected);
    });

    test('sending the same sale twice records it once (the server is idempotent)', () async {
      await queue.enqueue(_entry('dup'));
      await coordinator.drain();
      await queue.enqueue(_entry('dup')); // e.g. an ambiguous earlier attempt already landed
      await coordinator.drain();

      expect(server.recorded, hasLength(1));
    });

    test('overlapping drains do not send a sale twice', () async {
      await queue.enqueue(_entry('a'));
      await Future.wait([coordinator.drain(), coordinator.drain(), coordinator.drain()]);
      expect(server.requests, hasLength(1));
    });

    test('starting up retries rows a crash left mid-send', () async {
      await queue.enqueue(_entry('stuck', status: SaleQueueStatus.syncing));

      coordinator.start();
      await Future<void>.delayed(const Duration(milliseconds: 50));
      coordinator.dispose();

      expect(queue['stuck']!.status, SaleQueueStatus.synced);
    });
  });

  group('DriftSaleQueueStore', () {
    late AppDatabase database;

    DriftSaleQueueStore storeFor() => DriftSaleQueueStore(
      dao: database.queuedSaleDao,
      identityDao: database.deviceIdentityDao,
    );

    Future<void> signInAs(String tenant, {String device = 'd1'}) =>
        database.deviceIdentityDao.saveIdentity(
          deviceId: device,
          tenantId: tenant,
          branchId: 'b',
        );

    setUp(() => database = AppDatabase.forTesting(NativeDatabase.memory()));
    tearDown(() => database.close());

    test('a queued sale round-trips, including the whole checkout request', () async {
      await signInAs('tenant-a');
      final store = storeFor();
      await store.enqueue(_entry('s1', number: 7));

      final loaded = (await store.pending()).single;

      expect(loaded.saleId, 's1');
      expect(loaded.receiptNumber, 7);
      expect(loaded.request.offlineSale, isTrue);
      expect(loaded.request.receiptNumber, 7);
      expect(loaded.request.lines.single.itemId, 'coffee');
      expect(loaded.request.payment.method, PaymentMethod.cash);
      expect(loaded.request.payment.amountTendered, 100);
      expect(loaded.request.soldAt, isNotNull);
    });

    test('status changes and attempt counting persist', () async {
      await signInAs('tenant-a');
      final store = storeFor();
      await store.enqueue(_entry('s1'));

      await store.markSyncing('s1');
      expect(await store.pending(), isEmpty, reason: 'syncing rows are not re-sent concurrently');
      await store.resetStuck();
      expect((await store.pending()).single.saleId, 's1');

      await store.markPending('s1', error: 'offline');
      final again = (await store.pending()).single;
      expect(again.attempts, 1);
      expect(again.lastError, 'offline');

      await store.markRejected('s1', 'item removed');
      expect(await store.pending(), isEmpty);
      expect((await store.reviewable()).single.status, SaleQueueStatus.rejected);

      await store.dismiss('s1');
      expect(await store.reviewable(), isEmpty);
    });

    test('synced sales leave the review list', () async {
      await signInAs('tenant-a');
      final store = storeFor();
      await store.enqueue(_entry('s1'));
      await store.markSynced('s1');

      expect(await store.pending(), isEmpty);
      expect(await store.reviewable(), isEmpty);
    });

    test('one tenant never sees, counts or sends another tenant sales on a shared device', () async {
      await signInAs('tenant-a');
      await storeFor().enqueue(_entry('a-sale'));

      await signInAs('tenant-b');
      final other = storeFor();
      expect(await other.pending(), isEmpty);
      expect(await other.reviewable(), isEmpty);
      await other.enqueue(_entry('b-sale', number: 1));

      await signInAs('tenant-a');
      final back = storeFor();
      expect((await back.pending()).map((e) => e.saleId), ['a-sale']);
    });

    test('queuing a sale before the terminal has an identity is refused, not misfiled', () async {
      await expectLater(storeFor().enqueue(_entry('s1')), throwsStateError);
    });
  });
}
