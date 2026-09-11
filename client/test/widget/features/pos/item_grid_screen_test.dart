import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/item_grid_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_pos_repository.dart';

const _water = Item(
  id: 'item-1',
  name: 'Bottled Water',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 15,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
);

const _riceSack = Item(
  id: 'item-2',
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

Widget _wrap(
  FakeCatalogRepository catalogRepository,
  FakePosRepository posRepository,
) {
  return ProviderScope(
    overrides: [
      catalogRepositoryProvider.overrideWithValue(catalogRepository),
      posRepositoryProvider.overrideWithValue(posRepository),
    ],
    child: const MaterialApp(home: ItemGridScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no active items', (
    tester,
  ) async {
    await tester.pumpWidget(
      _wrap(FakeCatalogRepository(), FakePosRepository()),
    );
    await tester.pumpAndSettle();

    expect(find.text('No active items to sell yet.'), findsOneWidget);
  });

  testWidgets('tapping a unit-priced item adds it to the cart', (tester) async {
    final catalogRepository = FakeCatalogRepository(initialItems: [_water]);
    final posRepository = FakePosRepository();
    await tester.pumpWidget(_wrap(catalogRepository, posRepository));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Bottled Water'));
    await tester.pumpAndSettle();

    expect(posRepository.cart.lines, hasLength(1));
    expect(find.text('Added Bottled Water'), findsOneWidget);
  });

  testWidgets('tapping a non-unit item explains it is not addable yet', (
    tester,
  ) async {
    final catalogRepository = FakeCatalogRepository(initialItems: [_riceSack]);
    final posRepository = FakePosRepository();
    await tester.pumpWidget(_wrap(catalogRepository, posRepository));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Rice (Sack)'));
    await tester.pumpAndSettle();

    expect(posRepository.cart.lines, isEmpty);
    expect(find.textContaining('needs a customization step'), findsOneWidget);
  });
}
