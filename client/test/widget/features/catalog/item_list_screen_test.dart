import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/item_list_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: ItemListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no items yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeCatalogRepository()));
    await tester.pumpAndSettle();

    expect(
      find.text('No items yet — tap + to add your first one.'),
      findsOneWidget,
    );
  });

  testWidgets(
    'adding a unit-priced item from the + button refreshes the list',
    (tester) async {
      final repository = FakeCatalogRepository();
      await tester.pumpWidget(_wrap(repository));
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.add));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Item name'),
        'Bottled Water',
      );
      await tester.enterText(find.widgetWithText(TextFormField, 'Price'), '15');
      await tester.ensureVisible(find.widgetWithText(FilledButton, 'Add Item'));
      await tester.tap(find.widgetWithText(FilledButton, 'Add Item'));
      await tester.pumpAndSettle();

      expect(find.text('Bottled Water'), findsOneWidget);
      expect(repository.lastCreateItemRequest?.basePrice, 15);
    },
  );

  testWidgets('a negative price is rejected client-side before submitting', (
    tester,
  ) async {
    final repository = FakeCatalogRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Item name'),
      'Broken Item',
    );
    await tester.enterText(find.widgetWithText(TextFormField, 'Price'), '-5');
    await tester.ensureVisible(find.widgetWithText(FilledButton, 'Add Item'));
    await tester.tap(find.widgetWithText(FilledButton, 'Add Item'));
    await tester.pump();

    expect(find.text('Price cannot be negative'), findsOneWidget);
    expect(repository.lastCreateItemRequest, isNull);
  });

  testWidgets('shows a scan-barcode action in the app bar', (tester) async {
    await tester.pumpWidget(_wrap(FakeCatalogRepository()));
    await tester.pumpAndSettle();

    expect(find.byIcon(Icons.qr_code_scanner), findsOneWidget);
  });

  testWidgets(
    'the add-item form offers a scan-to-fill button on the barcode field',
    (tester) async {
      await tester.pumpWidget(_wrap(FakeCatalogRepository()));
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.add));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.qr_code_scanner), findsOneWidget);
    },
  );
}
