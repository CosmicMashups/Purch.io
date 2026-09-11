import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/low_stock_threshold_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

const _rice = Item(
  id: 'item-1',
  name: 'Rice',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 55,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 20,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp(home: LowStockThresholdScreen(item: _rice)),
  );
}

void main() {
  testWidgets('saving a threshold updates the item and pops', (tester) async {
    final repository = FakeCatalogRepository(initialItems: [_rice]);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), '10');
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();

    expect(repository.lastUpdateLowStockThresholdRequest?.threshold, 10);
    expect(repository.items.single.lowStockThreshold, 10);
  });

  testWidgets('clearing the field turns the alert off', (tester) async {
    const riceWithThreshold = Item(
      id: 'item-1',
      name: 'Rice',
      sku: null,
      barcode: null,
      categoryId: null,
      basePrice: 55,
      imageUrl: null,
      pricingType: PricingType.unit,
      stockOnHand: 20,
      isActive: true,
      tingiMode: TingiMode.none,
      packagedSize: null,
      tingiIncrementStep: null,
      tingiAllowedSizes: [],
      serviceDurationMinutes: null,
      departmentId: null,
      lowStockThreshold: 10,
    );
    final repository = FakeCatalogRepository(initialItems: [riceWithThreshold]);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
        child: MaterialApp(
          home: LowStockThresholdScreen(item: riceWithThreshold),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), '');
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();

    expect(repository.lastUpdateLowStockThresholdRequest?.threshold, isNull);
    expect(repository.items.single.lowStockThreshold, isNull);
  });
}
