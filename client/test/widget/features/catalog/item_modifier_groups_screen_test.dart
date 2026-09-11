import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/modifier_models.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/item_modifier_groups_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

Widget _wrap(FakeCatalogRepository repository) {
  return ProviderScope(
    overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(
      home: ItemModifierGroupsScreen(itemId: 'item-1', itemName: 'Iced Tea'),
    ),
  );
}

void main() {
  testWidgets('shows an empty state when no groups are attached yet', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeCatalogRepository()));
    await tester.pumpAndSettle();

    expect(
      find.text('No customization options attached yet — tap + to attach one.'),
      findsOneWidget,
    );
  });

  testWidgets(
    'attaching an existing modifier group from the + button shows it',
    (tester) async {
      final repository = FakeCatalogRepository(
        initialModifierGroups: [
          const ModifierGroup(
            id: 'group-1',
            name: 'Ice Level',
            allowMultipleSelection: false,
            isRequired: false,
            modifiers: [],
          ),
        ],
      );
      await tester.pumpWidget(_wrap(repository));
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.add));
      await tester.pumpAndSettle();

      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Ice Level').last);
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(FilledButton, 'Attach'));
      await tester.pumpAndSettle();

      expect(find.text('Ice Level'), findsOneWidget);
      expect(repository.itemModifierGroups['item-1'], hasLength(1));
    },
  );
}
