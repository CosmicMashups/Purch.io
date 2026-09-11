import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/modifier_models.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/screens/add_modifier_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';

void main() {
  testWidgets('adding an option to an existing group succeeds and pops', (
    tester,
  ) async {
    final repository = FakeCatalogRepository(
      initialModifierGroups: [
        const ModifierGroup(
          id: 'group-1',
          name: 'Add-ons',
          allowMultipleSelection: true,
          isRequired: false,
          modifiers: [],
        ),
      ],
    );

    var popped = false;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [catalogRepositoryProvider.overrideWithValue(repository)],
        child: MaterialApp(
          navigatorObservers: [_PopObserver(onPop: () => popped = true)],
          home: const AddModifierScreen(
            groupId: 'group-1',
            groupName: 'Add-ons',
          ),
        ),
      ),
    );

    await tester.enterText(
      find.widgetWithText(TextFormField, 'Option name (e.g. "Extra Cheese")'),
      'Extra Cheese',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Price add-on'),
      '15',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Add Option'));
    await tester.pumpAndSettle();

    expect(
      repository.modifierGroups.single.modifiers.single.name,
      'Extra Cheese',
    );
    expect(popped, isTrue);
  });
}

class _PopObserver extends NavigatorObserver {
  _PopObserver({required this.onPop});

  final VoidCallback onPop;

  @override
  void didPop(Route<dynamic> route, Route<dynamic>? previousRoute) => onPop();
}
