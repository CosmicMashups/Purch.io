import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/edit_item_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository, Item item) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp(home: EditItemScreen(item: item)),
  );
}

void main() {
  const sampleItem = Item(
    id: 'item-101',
    name: 'Bottled Water 500ml',
    sku: 'SKU-BW500',
    barcode: '480001234567',
    categoryId: null,
    basePrice: 15.00,
    imageUrl: null,
    pricingType: PricingType.unit,
    stockOnHand: 50.0,
    isActive: true,
    tingiMode: TingiMode.none,
    packagedSize: null,
    tingiIncrementStep: null,
    tingiAllowedSizes: [],
    serviceDurationMinutes: null,
    departmentId: null,
    lowStockThreshold: 10.0,
  );

  testWidgets('pre-populates item fields correctly', (tester) async {
    final repository = FakeCatalogRepository(initialItems: [sampleItem]);
    await tester.pumpWidget(_wrap(repository, sampleItem));
    await tester.pumpAndSettle();

    expect(find.text('Edit Item'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, 'Bottled Water 500ml'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, '15.00'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, 'SKU-BW500'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, '480001234567'), findsOneWidget);
    expect(find.text('Item is Active'), findsOneWidget);
  });

  testWidgets('updating fields and submitting calls repository.updateItem', (
    tester,
  ) async {
    final repository = FakeCatalogRepository(initialItems: [sampleItem]);
    await tester.pumpWidget(_wrap(repository, sampleItem));
    await tester.pumpAndSettle();

    final nameField = find.widgetWithText(TextFormField, 'Bottled Water 500ml');
    await tester.enterText(nameField, 'Bottled Water 1L');

    final priceField = find.widgetWithText(TextFormField, '15.00');
    await tester.enterText(priceField, '25.50');

    await tester.ensureVisible(find.widgetWithText(FilledButton, 'Save Changes'));
    await tester.tap(find.widgetWithText(FilledButton, 'Save Changes'));
    await tester.pumpAndSettle();

    expect(repository.lastUpdateItemRequest, isNotNull);
    expect(repository.lastUpdateItemRequest!.name, 'Bottled Water 1L');
    expect(repository.lastUpdateItemRequest!.basePrice, 25.50);
    expect(repository.lastUpdateItemRequest!.isActive, isTrue);
  });

  testWidgets('rejects negative price on edit', (tester) async {
    final repository = FakeCatalogRepository(initialItems: [sampleItem]);
    await tester.pumpWidget(_wrap(repository, sampleItem));
    await tester.pumpAndSettle();

    final priceField = find.widgetWithText(TextFormField, '15.00');
    await tester.enterText(priceField, '-10');

    await tester.ensureVisible(find.widgetWithText(FilledButton, 'Save Changes'));
    await tester.tap(find.widgetWithText(FilledButton, 'Save Changes'));
    await tester.pump();

    expect(find.text('Price cannot be negative'), findsOneWidget);
    expect(repository.lastUpdateItemRequest, isNull);
  });
}
