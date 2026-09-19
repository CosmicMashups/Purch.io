import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/data/sale_queue.dart';
import 'package:purch_client/features/pos/domain/offline_limits.dart';
import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/widgets/offline_sales_banner.dart';

SaleQueueEntry _entry(
  String id, {
  int number = 1,
  SaleQueueStatus status = SaleQueueStatus.pending,
  DateTime? soldAt,
  String? error,
}) => SaleQueueEntry(
  saleId: id,
  receiptNumber: number,
  totalAmount: 250,
  soldAt: soldAt ?? DateTime.now(),
  status: status,
  lastError: error,
  request: CheckoutRequest(
    saleId: id,
    lines: const [],
    seniorPwdDiscountApplied: false,
    promoCode: null,
    orderType: null,
    payment: const RecordPaymentRequest(method: PaymentMethod.cash),
  ),
);

Widget _wrap(List<SaleQueueEntry> entries, {OfflineLimits limits = const OfflineLimits()}) {
  return ProviderScope(
    overrides: [
      offlineSalesProvider.overrideWith((ref) => Stream.value(entries)),
    ],
    child: MaterialApp(
      home: Scaffold(body: Column(children: [OfflineSalesBanner(limits: limits)])),
    ),
  );
}

void main() {
  testWidgets('shows nothing when no sales are waiting', (tester) async {
    await tester.pumpWidget(_wrap(const []));
    await tester.pumpAndSettle();
    expect(find.byIcon(Icons.cloud_off_rounded), findsNothing);
    expect(find.byIcon(Icons.error_outline_rounded), findsNothing);
  });

  testWidgets('tells the cashier how many sales are waiting to sync', (tester) async {
    await tester.pumpWidget(_wrap([_entry('a'), _entry('b', number: 2)]));
    await tester.pumpAndSettle();
    expect(find.textContaining('2 sales saved offline'), findsOneWidget);
  });

  testWidgets('warns as the terminal nears its limit', (tester) async {
    await tester.pumpWidget(
      _wrap(
        [for (var i = 0; i < 4; i++) _entry('s$i', number: i + 1)],
        limits: const OfflineLimits(maxSales: 5),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('reconnect soon'), findsOneWidget);
  });

  testWidgets('says selling is blocked at the limit', (tester) async {
    await tester.pumpWidget(
      _wrap(
        [for (var i = 0; i < 5; i++) _entry('s$i', number: i + 1)],
        limits: const OfflineLimits(maxSales: 5),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('limit reached'), findsOneWidget);
  });

  testWidgets('flags a refused sale for review and lists the reason in the sheet', (tester) async {
    await tester.pumpWidget(
      _wrap([
        _entry(
          'bad',
          number: 9,
          status: SaleQueueStatus.rejected,
          error: 'This item is not active.',
        ),
      ]),
    );
    await tester.pumpAndSettle();

    expect(find.textContaining('need'), findsOneWidget);
    expect(find.textContaining('review'), findsOneWidget);

    await tester.tap(find.byType(InkWell).first);
    await tester.pumpAndSettle();

    expect(find.text('Offline sales'), findsOneWidget);
    expect(find.textContaining('Receipt No. 9'), findsOneWidget);
    expect(find.textContaining('This item is not active.'), findsOneWidget);
    expect(find.text('Mark seen'), findsOneWidget);
  });
}
