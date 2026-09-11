import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/tingi_config_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

const _riceItem = Item(
  id: 'item-1',
  name: 'Rice (Sack)',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 60,
  imageUrl: null,
  pricingType: PricingType.weightVolume,
  stockOnHand: 50,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
);

void main() {
  testWidgets('setting fixed tingi sizes succeeds and pops', (tester) async {
    final repository = FakeCatalogRepository(initialItems: [_riceItem]);

    var popped = false;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
        child: MaterialApp(
          navigatorObservers: [_PopObserver(onPop: () => popped = true)],
          home: TingiConfigScreen(item: _riceItem),
        ),
      ),
    );

    await tester.tap(find.text('Fixed sizes'));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(
        TextFormField,
        'Whole pack size (e.g. 50 for a 50kg sack)',
      ),
      '1',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Size (e.g. 10)'),
      '0.5',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();

    final updated = repository.items.single;
    expect(updated.tingiMode, TingiMode.fixedSizes);
    expect(updated.tingiAllowedSizes, [0.5]);
    expect(popped, isTrue);
  });

  testWidgets('submitting fixed sizes with no pack size shows a snackbar', (
    tester,
  ) async {
    final repository = FakeCatalogRepository(initialItems: [_riceItem]);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
        child: MaterialApp(home: TingiConfigScreen(item: _riceItem)),
      ),
    );

    await tester.tap(find.text('Fixed sizes'));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Size (e.g. 10)'),
      '0.5',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pump();

    expect(
      find.text('Enter a valid whole pack size greater than zero.'),
      findsOneWidget,
    );
    expect(repository.items.single.tingiMode, TingiMode.none);
  });
}

class _PopObserver extends NavigatorObserver {
  _PopObserver({required this.onPop});

  final VoidCallback onPop;

  @override
  void didPop(Route<dynamic> route, Route<dynamic>? previousRoute) => onPop();
}
