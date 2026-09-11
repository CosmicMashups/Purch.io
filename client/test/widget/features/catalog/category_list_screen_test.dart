import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/category_list_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: CategoryListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no categories yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeCatalogRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No categories yet — tap + to add one.'), findsOneWidget);
  });

  testWidgets('adding a category from the + button refreshes the list', (
    tester,
  ) async {
    final repository = FakeCatalogRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Category name'),
      'Beverages',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Add Category'));
    await tester.pumpAndSettle();

    expect(find.text('Beverages'), findsOneWidget);
  });
}
