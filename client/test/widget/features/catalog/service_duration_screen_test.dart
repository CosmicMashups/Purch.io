import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/service_duration_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

const _haircutItem = Item(
  id: 'item-1',
  name: 'Haircut',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 250,
  imageUrl: null,
  pricingType: PricingType.service,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

void main() {
  testWidgets('setting a valid duration succeeds and pops', (tester) async {
    final repository = FakeCatalogRepository(initialItems: [_haircutItem]);

    var popped = false;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
        child: MaterialApp(
          navigatorObservers: [_PopObserver(onPop: () => popped = true)],
          home: ServiceDurationScreen(item: _haircutItem),
        ),
      ),
    );

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Duration (minutes)'),
      '45',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();

    expect(repository.items.single.serviceDurationMinutes, 45);
    expect(popped, isTrue);
  });

  testWidgets('a zero duration is rejected client-side', (tester) async {
    final repository = FakeCatalogRepository(initialItems: [_haircutItem]);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
        child: MaterialApp(home: ServiceDurationScreen(item: _haircutItem)),
      ),
    );

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Duration (minutes)'),
      '0',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pump();

    expect(find.text('Enter a duration greater than zero'), findsOneWidget);
    expect(repository.items.single.serviceDurationMinutes, isNull);
  });
}

class _PopObserver extends NavigatorObserver {
  _PopObserver({required this.onPop});

  final VoidCallback onPop;

  @override
  void didPop(Route<dynamic> route, Route<dynamic>? previousRoute) => onPop();
}
