import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/pos/data/local_first_pos_repository.dart';
import 'package:purch_client/features/pos/data/sale_queue.dart';
import 'package:purch_client/features/pos/data/sale_sync_coordinator.dart';
import 'package:purch_client/features/pos/domain/bir_reading_models.dart';
import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/pos_repository.dart';
import 'package:purch_client/features/pos/domain/pricing_engine.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';
import 'package:purch_client/features/pos/presentation/providers/bir_reading_providers.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';

import '../../../helpers/fake_bir_reading_repository.dart';
import '../../../helpers/fake_catalog_repository.dart';

const _coffee = Item(
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

/// The order in which sales reached the server is the whole point of these tests.
class _Server implements PosRepository {
  Object? failure;
  final receivedReceiptNumbers = <int?>[];

  @override
  Future<Transaction> checkout(CheckoutRequest request) async {
    if (failure != null) {
      throw failure!;
    }
    receivedReceiptNumbers.add(request.receiptNumber);
    return Transaction(
      id: 'srv-${request.saleId}',
      branchId: 'b',
      deviceId: 'd',
      status: TransactionStatus.completed,
      lines: const [],
      subtotal: 0,
      discountAmount: 0,
      seniorPwdDiscountApplied: false,
      promoCode: null,
      promoDiscountAmount: 0,
      totalAmount: request.expectedTotal ?? 0,
      receiptNumber: request.receiptNumber,
      payments: const [],
    );
  }

  @override
  Future<int> getLastIssuedReceiptNumber() async => 0;

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

SaleQueueEntry _queued(String id, int number) => SaleQueueEntry(
  saleId: id,
  receiptNumber: number,
  totalAmount: 100,
  soldAt: DateTime(2026, 9, 20, 9, number),
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
    soldAt: DateTime(2026, 9, 20, 9, number),
  ),
);

void main() {
  group('sales keep the order they were rung up', () {
    late _Server server;
    late MemorySaleQueueStore queue;
    late SaleSyncCoordinator coordinator;
    late int localCounter;

    LocalFirstPosRepository build() => LocalFirstPosRepository(
      remote: server,
      catalog: FakeCatalogRepository(),
      loadItems: () async => [_coffee],
      loadRules: () async => PricingRules.empty,
      store: MemoryCartDraftStore(),
      identity: () async => null,
      lastIssuedReceiptNumber: ({bool refresh = false}) async => localCounter,
      recordReceiptNumber: (n) async => localCounter = n > localCounter ? n : localCounter,
      saleQueue: queue,
      isConnected: () async => true,
      drainQueue: () => coordinator.drain(),
      clock: () => DateTime(2026, 9, 20, 10, 0),
    );

    const cash = RecordPaymentRequest(
      method: PaymentMethod.cash,
      amountTendered: 500,
    );

    setUp(() {
      server = _Server();
      queue = MemorySaleQueueStore();
      coordinator = SaleSyncCoordinator(store: queue, remote: server, onSynced: () {});
      localCounter = 12; // sales 10-12 were rung up offline earlier
    });

    test('when the connection returns, waiting sales are sent BEFORE the next online sale', () async {
      await queue.enqueue(_queued('s10', 10));
      await queue.enqueue(_queued('s11', 11));
      await queue.enqueue(_queued('s12', 12));

      final repo = build();
      await repo.addLine(const AddTransactionLineRequest(itemId: 'coffee', quantity: 1));
      final sale = await repo.recordPayment(cash);

      // Without the guard, 13 would reach the server first and a Z-reading in
      // between would close over it, leaving 10-12 behind the reading.
      expect(server.receivedReceiptNumbers, [10, 11, 12, 13]);
      expect(sale.receiptNumber, 13);
      expect(sale.savedOffline, isFalse);
    });

    test('if the waiting sales cannot be sent, the new sale queues behind them instead of overtaking', () async {
      await queue.enqueue(_queued('s10', 10));
      server.failure = const NetworkFailure('still offline');

      final repo = build();
      await repo.addLine(const AddTransactionLineRequest(itemId: 'coffee', quantity: 1));
      final sale = await repo.recordPayment(cash);

      expect(sale.savedOffline, isTrue);
      expect(sale.receiptNumber, 13);
      expect(server.receivedReceiptNumbers, isEmpty);
      expect(
        (await queue.pending()).map((e) => e.receiptNumber),
        [10, 13],
        reason: 'one queue, in receipt-number order',
      );
    });

    test('a payment that needs the server will not jump ahead of waiting offline sales', () async {
      await queue.enqueue(_queued('s10', 10));
      server.failure = const NetworkFailure('still offline');

      final repo = build();
      await repo.addLine(const AddTransactionLineRequest(itemId: 'coffee', quantity: 1));

      await expectLater(
        repo.recordPayment(
          const RecordPaymentRequest(
            method: PaymentMethod.utangCredit,
            customerCreditLedgerId: 'ledger',
          ),
        ),
        throwsA(isA<NetworkFailure>()),
      );

      expect(server.receivedReceiptNumbers, isEmpty);
      expect((await queue.pending()).map((e) => e.receiptNumber), [10]);
      // The cart is intact and its reserved number is still free.
      expect((await repo.getOrCreateOpenCart()).lines, hasLength(1));
    });

    test('with nothing waiting, payment goes straight to the server as before', () async {
      final repo = build();
      await repo.addLine(const AddTransactionLineRequest(itemId: 'coffee', quantity: 1));
      final sale = await repo.recordPayment(cash);

      expect(server.receivedReceiptNumbers, [13]);
      expect(sale.savedOffline, isFalse);
    });
  });

  group('Z-reading with offline sales', () {
    late _Server server;
    late MemorySaleQueueStore queue;
    late FakeBirReadingRepository readings;
    late ProviderContainer container;

    setUp(() {
      server = _Server();
      queue = MemorySaleQueueStore();
      readings = FakeBirReadingRepository();
      container = ProviderContainer(
        overrides: [
          saleQueueStoreProvider.overrideWithValue(queue),
          saleSyncCoordinatorProvider.overrideWith(
            (ref) => SaleSyncCoordinator(store: queue, remote: server, onSynced: () {}),
          ),
          birReadingRepositoryProvider.overrideWithValue(readings),
        ],
      );
      addTearDown(container.dispose);
    });

    test('runs normally when nothing is waiting', () async {
      final controller = container.read(generateBirReadingControllerProvider.notifier);

      expect(await controller.generateZReading(), isTrue);
      expect(readings.zReadingCallCount, 1);
    });

    test('syncs waiting offline sales first, then runs', () async {
      await queue.enqueue(_queued('s1', 1));
      final controller = container.read(generateBirReadingControllerProvider.notifier);

      expect(await controller.generateZReading(), isTrue);

      expect(server.receivedReceiptNumbers, [1]);
      expect(readings.zReadingCallCount, 1);
    });

    test('refuses to run while offline sales still cannot be synced, and says why', () async {
      await queue.enqueue(_queued('s1', 1));
      await queue.enqueue(_queued('s2', 2));
      server.failure = const NetworkFailure('offline');
      final controller = container.read(generateBirReadingControllerProvider.notifier);

      expect(await controller.generateZReading(), isFalse);

      expect(readings.zReadingCallCount, 0, reason: 'the reading must not close the day yet');
      final failure = controller.currentFailure;
      expect(failure, isA<ConflictFailure>());
      expect(failure!.message, contains('2 sales'));
    });

    test('an X-reading is informational and is never blocked', () async {
      await queue.enqueue(_queued('s1', 1));
      server.failure = const NetworkFailure('offline');
      final controller = container.read(generateBirReadingControllerProvider.notifier);

      expect(await controller.generateXReading(), isTrue);
    });

    test('a refused sale does not block the Z-reading (it will never sync)', () async {
      await queue.enqueue(_queued('s1', 1));
      await queue.markRejected('s1', 'item removed');
      final controller = container.read(generateBirReadingControllerProvider.notifier);

      expect(await controller.generateZReading(), isTrue);
    });
  });

  group('BirReading model', () {
    test('reads the late and missing receipt numbers, and tolerates their absence', () {
      Map<String, dynamic> json({Object? late, Object? missing}) => {
        'type': 1,
        'deviceId': 'd',
        'machineIdentificationNumber': 'MIN',
        'generatedAt': '2026-09-20T10:00:00Z',
        'beginningReceiptNumber': 2,
        'endingReceiptNumber': 5,
        'transactionCount': 1,
        'grossSales': 1,
        'vatableSales': 1,
        'vatAmount': 0,
        'seniorPwdDiscountTotal': 0,
        'promoDiscountTotal': 0,
        'totalDiscounts': 0,
        'netSales': 1,
        'voidedCount': 0,
        'voidedAmount': 0,
        'oldGrandAccumulatedSales': 0,
        'newGrandAccumulatedSales': 1,
        'resetCounter': 1,
        'lateReceiptNumbers': late,
        'missingReceiptNumbers': missing,
      };

      final full = BirReading.fromJson(json(late: [2], missing: [3, 4]));
      expect(full.lateReceiptNumbers, [2]);
      expect(full.missingReceiptNumbers, [3, 4]);

      final older = BirReading.fromJson(json());
      expect(older.lateReceiptNumbers, isEmpty);
      expect(older.missingReceiptNumbers, isEmpty);
    });
  });
}
