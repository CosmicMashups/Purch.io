import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/modifier_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/item_modifier_customization_dialog.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_pos_repository.dart';

const _milkTea = Item(
  id: 'item-milk-tea',
  name: 'Milk Tea',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 100,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 10,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

const _sugarLevelGroup = ModifierGroup(
  id: 'group-sugar',
  name: 'Sugar Level',
  allowMultipleSelection: false,
  isRequired: true,
  modifiers: [
    ItemModifierOption(id: 'mod-sugar-100', name: '100% Sugar', priceDelta: 0),
    ItemModifierOption(id: 'mod-sugar-50', name: '50% Sugar', priceDelta: 0),
  ],
);

const _toppingsGroup = ModifierGroup(
  id: 'group-toppings',
  name: 'Toppings',
  allowMultipleSelection: true,
  isRequired: false,
  modifiers: [
    ItemModifierOption(id: 'mod-pearls', name: 'Pearls', priceDelta: 15),
    ItemModifierOption(id: 'mod-pudding', name: 'Pudding', priceDelta: 20),
  ],
);

Widget _buildTestWidget({
  required FakeCatalogRepository catalogRepo,
  required FakePosRepository posRepo,
  void Function(AddTransactionLineRequest)? onAdd,
}) {
  return ProviderScope(
    overrides: [
      catalogRepositoryProvider.overrideWithValue(catalogRepo),
      posRepositoryProvider.overrideWithValue(posRepo),
    ],
    child: MaterialApp(
      home: Scaffold(
        body: ItemModifierCustomizationDialog(
          item: _milkTea,
          addLine: onAdd != null
              ? (req) async {
                  onAdd(req);
                  return true;
                }
              : null,
        ),
      ),
    ),
  );
}

void main() {
  testWidgets(
    'disables Add to Cart until required modifier group is selected',
    (tester) async {
      final catalogRepo = FakeCatalogRepository(
        initialItemModifierGroups: {
          _milkTea.id: [_sugarLevelGroup, _toppingsGroup],
        },
      );
      final posRepo = FakePosRepository();

      await tester.pumpWidget(
        _buildTestWidget(catalogRepo: catalogRepo, posRepo: posRepo),
      );
      await tester.pumpAndSettle();

      expect(find.text('Sugar Level'), findsOneWidget);
      expect(find.text('Toppings'), findsOneWidget);
      expect(find.text('Select Required Options'), findsOneWidget);

      // Tap an option in the required sugar level group
      await tester.tap(find.text('100% Sugar'));
      await tester.pumpAndSettle();

      // Now it should be enabled and show price
      expect(find.text('Add to Cart — ₱100.00'), findsOneWidget);

      // Add a topping (+₱15.00)
      await tester.tap(find.text('Pearls (+₱15.00)'));
      await tester.pumpAndSettle();

      expect(find.text('Add to Cart — ₱115.00'), findsOneWidget);
    },
  );

  testWidgets(
    'submits selectedModifierIds when Add to Cart is tapped',
    (tester) async {
      final catalogRepo = FakeCatalogRepository(
        initialItemModifierGroups: {
          _milkTea.id: [_sugarLevelGroup, _toppingsGroup],
        },
      );
      final posRepo = FakePosRepository();
      AddTransactionLineRequest? capturedRequest;

      await tester.pumpWidget(
        _buildTestWidget(
          catalogRepo: catalogRepo,
          posRepo: posRepo,
          onAdd: (req) => capturedRequest = req,
        ),
      );
      await tester.pumpAndSettle();

      // Select required sugar level + optional pearl topping
      await tester.tap(find.text('50% Sugar'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Pearls (+₱15.00)'));
      await tester.pumpAndSettle();

      // Increase quantity to 2
      await tester.tap(find.byIcon(Icons.add));
      await tester.pumpAndSettle();

      expect(find.text('Add to Cart — ₱230.00'), findsOneWidget);

      await tester.tap(find.text('Add to Cart — ₱230.00'));
      await tester.pumpAndSettle();

      expect(capturedRequest, isNotNull);
      expect(capturedRequest!.itemId, _milkTea.id);
      expect(capturedRequest!.quantity, 2.0);
      expect(capturedRequest!.selectedModifierIds, containsAll(['mod-sugar-50', 'mod-pearls']));
    },
  );
}
