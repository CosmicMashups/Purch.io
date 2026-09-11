import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/combo_components_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(
      home: ComboComponentsScreen(itemId: 'item-1', itemName: 'Value Meal'),
    ),
  );
}

void main() {
  testWidgets('shows an empty state when no combo slots exist yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeCatalogRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No combo slots yet — tap + to add one.'), findsOneWidget);
  });

  testWidgets('adding a combo slot from the + button shows the new slot', (
    tester,
  ) async {
    final repository = FakeCatalogRepository(
      initialCategories: [
        const Category(id: 'category-1', name: 'Drinks', sortOrder: 1),
      ],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Slot label (e.g. "Choose a Drink")'),
      'Choose a Drink',
    );
    await tester.tap(find.byType(DropdownButtonFormField<String>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Drinks').last);
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Quantity'), '1');
    await tester.tap(find.widgetWithText(FilledButton, 'Add Combo Slot'));
    await tester.pumpAndSettle();

    expect(find.text('Choose a Drink'), findsOneWidget);
    expect(repository.comboComponents.single.componentCategoryName, 'Drinks');
  });

  testWidgets('a zero quantity is rejected client-side', (tester) async {
    final repository = FakeCatalogRepository(
      initialCategories: [
        const Category(id: 'category-1', name: 'Sides', sortOrder: 1),
      ],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Slot label (e.g. "Choose a Drink")'),
      'Choose a Side',
    );
    await tester.tap(find.byType(DropdownButtonFormField<String>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sides').last);
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Quantity'), '0');
    await tester.tap(find.widgetWithText(FilledButton, 'Add Combo Slot'));
    await tester.pump();

    expect(find.text('Enter a quantity of 1 or more'), findsOneWidget);
    expect(repository.comboComponents, isEmpty);
  });
}
