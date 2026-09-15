import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/edit_category_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository, Category category) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp(home: EditCategoryScreen(category: category)),
  );
}

void main() {
  const sampleCategory = Category(
    id: 'cat-1',
    name: 'Beverages',
    sortOrder: 1,
    imageUrl: 'https://example.com/bev.png',
  );

  testWidgets('pre-populates category fields correctly', (tester) async {
    final repository = FakeCatalogRepository(initialCategories: [sampleCategory]);
    await tester.pumpWidget(_wrap(repository, sampleCategory));
    await tester.pumpAndSettle();

    expect(find.text('Edit Category'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, 'Beverages'), findsOneWidget);
    expect(find.widgetWithText(TextFormField, '1'), findsOneWidget);
  });

  testWidgets('submitting valid updates calls repository.updateCategory', (
    tester,
  ) async {
    final repository = FakeCatalogRepository(initialCategories: [sampleCategory]);
    await tester.pumpWidget(_wrap(repository, sampleCategory));
    await tester.pumpAndSettle();

    final nameField = find.widgetWithText(TextFormField, 'Beverages');
    await tester.enterText(nameField, 'Refreshing Drinks');

    final sortField = find.widgetWithText(TextFormField, '1');
    await tester.enterText(sortField, '3');

    await tester.tap(find.widgetWithText(FilledButton, 'Save Changes'));
    await tester.pumpAndSettle();

    expect(repository.lastUpdateCategoryRequest, isNotNull);
    expect(repository.lastUpdateCategoryRequest!.name, 'Refreshing Drinks');
    expect(repository.lastUpdateCategoryRequest!.sortOrder, 3);
  });

  testWidgets('validates required fields', (tester) async {
    final repository = FakeCatalogRepository(initialCategories: [sampleCategory]);
    await tester.pumpWidget(_wrap(repository, sampleCategory));
    await tester.pumpAndSettle();

    final nameField = find.widgetWithText(TextFormField, 'Beverages');
    await tester.enterText(nameField, '   ');

    await tester.tap(find.widgetWithText(FilledButton, 'Save Changes'));
    await tester.pump();

    expect(find.text('Category name is required'), findsOneWidget);
    expect(repository.lastUpdateCategoryRequest, isNull);
  });
}
