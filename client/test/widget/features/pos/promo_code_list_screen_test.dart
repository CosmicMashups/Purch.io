import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/domain/promo_code_models.dart';
import 'package:purch_client/features/pos/presentation/providers/promo_code_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/promo_code_list_screen.dart';

import '../../../helpers/fake_promo_code_repository.dart';

const _save10 = PromoCode(
  id: 'promo-1',
  code: 'SAVE10',
  discountType: PromoDiscountType.percentage,
  discountValue: 10,
  isActive: true,
  expiresAt: null,
);

Widget _wrap(FakePromoCodeRepository repository) {
  return ProviderScope(
    overrides: [promoCodeRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: PromoCodeListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no promo codes', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakePromoCodeRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No promo codes yet — tap + to add one.'), findsOneWidget);
  });

  testWidgets('lists promo codes with their discount label', (tester) async {
    final repository = FakePromoCodeRepository(initialCodes: [_save10]);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('SAVE10'), findsOneWidget);
    expect(find.text('10% off'), findsOneWidget);
  });

  testWidgets('adding a promo code from the + button refreshes the list', (
    tester,
  ) async {
    final repository = FakePromoCodeRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Code (e.g. SAVE10)'),
      'BIG20',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Percent off'),
      '20',
    );
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Add Promo Code'));
    await tester.pumpAndSettle();

    expect(repository.lastCreateRequest?.code, 'BIG20');
    expect(find.text('BIG20'), findsOneWidget);
  });
}
