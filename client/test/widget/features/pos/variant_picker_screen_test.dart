import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/item_variant_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/variant_picker_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_pos_repository.dart';

const _tShirt = Item(
  id: 'item-1',
  name: 'T-Shirt',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 200,
  imageUrl: null,
  pricingType: PricingType.variantMatrix,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
);

const _largeRed = ItemVariant(
  id: 'variant-1',
  attributes: {'size': 'L', 'color': 'Red'},
  sku: null,
  stockOnHand: 10,
  priceOverride: 220,
  imageUrl: null,
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
    child: MaterialApp(home: VariantPickerScreen(item: _tShirt)),
  );
}

void main() {
  testWidgets('tapping a variant adds it to the cart with its own price', (
    tester,
  ) async {
    final catalogRepository = FakeCatalogRepository(
      initialVariants: [_largeRed],
    );
    final posRepository = FakePosRepository();
    await tester.pumpWidget(_wrap(catalogRepository, posRepository));
    await tester.pumpAndSettle();

    expect(find.text('size: L, color: Red'), findsOneWidget);
    expect(find.text('₱220.00'), findsOneWidget);

    await tester.tap(find.text('size: L, color: Red'));
    await tester.pumpAndSettle();

    expect(posRepository.lastAddLineRequest?.itemVariantId, 'variant-1');
  });
}
