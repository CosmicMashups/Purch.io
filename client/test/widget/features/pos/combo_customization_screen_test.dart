import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_combo_component_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/combo_customization_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_pos_repository.dart';

const _valueMeal = Item(
  id: 'item-1',
  name: 'Value Meal',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 150,
  imageUrl: null,
  pricingType: PricingType.combo,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
);

const _soda = Item(
  id: 'item-2',
  name: 'Soda',
  sku: null,
  barcode: null,
  categoryId: 'category-drinks',
  basePrice: 25,
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

const _juice = Item(
  id: 'item-3',
  name: 'Juice',
  sku: null,
  barcode: null,
  categoryId: 'category-drinks',
  basePrice: 30,
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

const _drinkSlot = ItemComboComponent(
  id: 'slot-1',
  componentCategoryId: 'category-drinks',
  componentCategoryName: 'Drinks',
  slotLabel: 'Choose a Drink',
  quantity: 1,
  substitutionUpchargeAmount: null,
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
    child: MaterialApp(home: ComboCustomizationScreen(item: _valueMeal)),
  );
}

void main() {
  testWidgets('Add to Cart stays disabled until every slot is filled', (
    tester,
  ) async {
    final catalogRepository = FakeCatalogRepository(
      initialItems: [_soda, _juice],
      initialComboComponents: [_drinkSlot],
    );
    final posRepository = FakePosRepository();
    await tester.pumpWidget(_wrap(catalogRepository, posRepository));
    await tester.pumpAndSettle();

    final addButton = find.widgetWithText(FilledButton, 'Add to Cart');
    expect(tester.widget<FilledButton>(addButton).onPressed, isNull);

    await tester.tap(find.text('Soda'));
    await tester.pumpAndSettle();

    expect(tester.widget<FilledButton>(addButton).onPressed, isNotNull);
  });

  testWidgets('confirming adds the combo with the picked selection', (
    tester,
  ) async {
    final catalogRepository = FakeCatalogRepository(
      initialItems: [_soda, _juice],
      initialComboComponents: [_drinkSlot],
    );
    final posRepository = FakePosRepository();
    await tester.pumpWidget(_wrap(catalogRepository, posRepository));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Soda'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Add to Cart'));
    await tester.pumpAndSettle();

    final request = posRepository.lastAddLineRequest;
    expect(request?.itemId, 'item-1');
    final selection = request!.comboSelections!.single;
    expect(selection.slotId, 'slot-1');
    expect(selection.selectedItemId, 'item-2');
  });
}
