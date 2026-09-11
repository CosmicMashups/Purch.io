import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/modifier_group_list_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: ModifierGroupListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no modifier groups yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeCatalogRepository()));
    await tester.pumpAndSettle();

    expect(
      find.text('No modifier groups yet — tap + to add one.'),
      findsOneWidget,
    );
  });

  testWidgets('adding a group from the + button refreshes the list', (
    tester,
  ) async {
    final repository = FakeCatalogRepository();
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Group name (e.g. "Add-ons")'),
      'Add-ons',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Add Group'));
    await tester.pumpAndSettle();

    expect(find.text('Add-ons'), findsOneWidget);
  });

  testWidgets(
    'toggling required on a new group is reflected in the created request',
    (tester) async {
      final repository = FakeCatalogRepository();
      await tester.pumpWidget(_wrap(repository));
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.add));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Group name (e.g. "Add-ons")'),
        'Sugar Level',
      );
      await tester.ensureVisible(
        find.widgetWithText(SwitchListTile, 'Require a selection'),
      );
      await tester.tap(
        find.widgetWithText(SwitchListTile, 'Require a selection'),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Add Group'));
      await tester.pumpAndSettle();

      expect(repository.modifierGroups.single.isRequired, isTrue);
      expect(find.textContaining('Required'), findsOneWidget);
    },
  );
}
