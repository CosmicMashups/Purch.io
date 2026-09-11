import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/bundle_rules_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(
      home: BundleRulesScreen(itemId: 'item-1', itemName: 'Snack Pack'),
    ),
  );
}

void main() {
  testWidgets('shows an empty state when no bundle rules exist yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeCatalogRepository()));
    await tester.pumpAndSettle();

    expect(
      find.text('No bundle rules yet — tap + to add one.'),
      findsOneWidget,
    );
  });

  testWidgets('adding a bundle rule from the + button shows the new rule', (
    tester,
  ) async {
    final repository = FakeCatalogRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Description'),
      'Buy 3 chips',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Buy quantity'),
      '3',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Bundle price'),
      '99',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Add Bundle Rule'));
    await tester.pumpAndSettle();

    expect(find.text('Buy 3 chips'), findsOneWidget);
    expect(repository.bundleRules.single.triggerQuantity, 3);
  });

  testWidgets('a trigger quantity below 2 is rejected client-side', (
    tester,
  ) async {
    final repository = FakeCatalogRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Description'),
      'Invalid rule',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Buy quantity'),
      '1',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Bundle price'),
      '99',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Add Bundle Rule'));
    await tester.pump();

    expect(find.text('Enter a quantity of 2 or more'), findsOneWidget);
    expect(repository.bundleRules, isEmpty);
  });
}
