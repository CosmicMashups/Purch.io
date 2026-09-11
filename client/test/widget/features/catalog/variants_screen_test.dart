import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/variants_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(
      home: VariantsScreen(itemId: 'item-1', itemName: 'T-Shirt'),
    ),
  );
}

void main() {
  testWidgets('shows an empty state when no variants exist yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeCatalogRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No variants yet — tap + to add one.'), findsOneWidget);
  });

  testWidgets('adding a variant from the + button shows the new variant', (
    tester,
  ) async {
    final repository = FakeCatalogRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Attribute (e.g. Size)'),
      'Size',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Value (e.g. Large)'),
      'Large',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Add Variant'));
    await tester.pumpAndSettle();

    expect(find.text('Size: Large'), findsOneWidget);
    expect(repository.variants.single.attributes, {'Size': 'Large'});
  });

  testWidgets('submitting with no attributes shows a validation snackbar', (
    tester,
  ) async {
    final repository = FakeCatalogRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, 'Add Variant'));
    await tester.pumpAndSettle();

    expect(find.text('Add at least one attribute.'), findsOneWidget);
    expect(repository.variants, isEmpty);
  });
}
