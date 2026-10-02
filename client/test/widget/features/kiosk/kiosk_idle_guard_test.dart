import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:purch_client/features/kiosk/presentation/providers/kiosk_providers.dart';
import 'package:purch_client/features/kiosk/presentation/widgets/kiosk_idle_guard.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';

import '../../../helpers/fake_kiosk_cart_repository.dart';

Transaction _cartWithOneLine() => Transaction(
  id: 'cart-1',
  branchId: 'branch-1',
  deviceId: 'device-1',
  status: TransactionStatus.open,
  lines: [
    TransactionLine(
      id: 'line-1',
      itemId: 'item-1',
      itemName: 'Rice Meal',
      itemVariantId: null,
      quantity: 1,
      unitPrice: 85,
      lineTotal: 85,
      comboSelections: const [],
    ),
  ],
  subtotal: 85,
  discountAmount: 0,
  seniorPwdDiscountApplied: false,
  promoCode: null,
  promoDiscountAmount: 0,
  totalAmount: 85,
  receiptNumber: null,
  payments: const [],
);

Widget _app({required FakeKioskCartRepository repository, required String initialLocation}) {
  // Watching the notifier here warms it up so the idle check (a bare `ref.read`,
  // which must not itself trigger the first build) sees an already-resolved cart.
  final router = GoRouter(
    initialLocation: initialLocation,
    routes: [
      GoRoute(
        path: '/kiosk',
        builder:
            (context, state) => Consumer(
              builder: (context, ref, _) {
                ref.watch(kioskCartNotifierProvider);
                return const Text('Kiosk screen');
              },
            ),
      ),
      GoRoute(path: '/other', builder: (context, state) => const Text('Other screen')),
    ],
  );
  return ProviderScope(
    overrides: [kioskCartRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp.router(
      routerConfig: router,
      builder: (context, child) => KioskIdleGuard(router: router, child: child!),
    ),
  );
}

void main() {
  testWidgets('resets the kiosk back to the landing screen after the idle timeout with items in the cart', (tester) async {
    final repository = FakeKioskCartRepository(initialCart: _cartWithOneLine());
    await tester.pumpWidget(_app(repository: repository, initialLocation: '/kiosk'));
    await tester.pumpAndSettle();

    await tester.pump(kioskIdleResetDuration + const Duration(seconds: 1));
    await tester.pumpAndSettle();

    expect(repository.cart.lines, isEmpty);
  });

  testWidgets('a tap resets the idle clock, so the cart survives past the original timeout', (tester) async {
    final repository = FakeKioskCartRepository(initialCart: _cartWithOneLine());
    await tester.pumpWidget(_app(repository: repository, initialLocation: '/kiosk'));
    await tester.pumpAndSettle();

    await tester.pump(kioskIdleResetDuration - const Duration(seconds: 10));
    await tester.tapAt(const Offset(10, 10));
    await tester.pump(const Duration(seconds: 20));

    // The tap pushed the deadline out, so the cart is still there well past the original window.
    expect(repository.cart.lines, hasLength(1));
  });

  testWidgets('does nothing while outside the kiosk flow, even with a stale kiosk cart', (tester) async {
    final repository = FakeKioskCartRepository(initialCart: _cartWithOneLine());
    await tester.pumpWidget(_app(repository: repository, initialLocation: '/other'));
    await tester.pumpAndSettle();

    await tester.pump(kioskIdleResetDuration + const Duration(seconds: 1));
    await tester.pumpAndSettle();

    expect(repository.cart.lines, hasLength(1));
  });
}
