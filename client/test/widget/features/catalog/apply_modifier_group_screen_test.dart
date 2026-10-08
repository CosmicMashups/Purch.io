import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/apply_modifier_group_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Item _item(String id, String name, String? categoryId) => Item(
  id: id,
  name: name,
  sku: null,
  barcode: null,
  categoryId: categoryId,
  basePrice: 50,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: const [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

FakeCatalogRepository _repository() => FakeCatalogRepository(
  initialCategories: const [Category(id: 'drinks', name: 'Drinks', sortOrder: 0)],
  initialItems: [_item('tea', 'Tea', 'drinks'), _item('cake', 'Cake', null)],
);

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(
      home: ApplyModifierGroupScreen(groupId: 'g1', groupName: 'Sweetness'),
    ),
  );
}

void main() {
  testWidgets('gives the group to every item of a chosen category', (
    tester,
  ) async {
    final repository = _repository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byType(DropdownButtonFormField<String?>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Drinks').last);
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Apply'));
    await tester.pumpAndSettle();

    expect(repository.bulkAttaches, hasLength(1));
    expect(repository.bulkAttaches.single.$1, 'g1');
    expect(repository.bulkAttaches.single.$2.categoryId, 'drinks');
    expect(repository.bulkAttaches.single.$2.itemIds, isNull);
  });

  testWidgets('gives the group to the items ticked', (tester) async {
    final repository = _repository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Selected items'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cake'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Apply'));
    await tester.pumpAndSettle();

    expect(repository.bulkAttaches.single.$2.itemIds, ['cake']);
    expect(repository.bulkAttaches.single.$2.categoryId, isNull);
  });

  testWidgets('Apply stays disabled until something is chosen', (tester) async {
    await tester.pumpWidget(_wrap(_repository()));
    await tester.pumpAndSettle();

    final button = tester.widget<FilledButton>(
      find.widgetWithText(FilledButton, 'Apply'),
    );
    expect(button.onPressed, isNull);
  });
}
