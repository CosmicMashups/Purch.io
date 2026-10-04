import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/inventory/domain/inventory_item_models.dart';
import 'package:purch_client/features/inventory/domain/recipe_selection.dart';
import 'package:purch_client/features/inventory/presentation/widgets/ingredient_recipe_field.dart';

InventoryItem _ingredient(String id, String name, String unit) => InventoryItem(
  id: id,
  name: name,
  sku: null,
  baseUnit: unit,
  packagingUnit: 'pack',
  packagingSize: 1,
  quantityOnHand: 0,
  lowStockThreshold: null,
  isAutoCreatedForItem: false,
  linkedItemId: null,
  isActive: true,
);

final _ingredients = [
  _ingredient('bun', 'Burger bun', 'pair'),
  _ingredient('patty', 'Burger patty', 'pc'),
  _ingredient('dressing', 'Dressing', 'mL'),
];

/// Hosts the field the way the item screens do: the screen owns the selection.
class _Host extends StatefulWidget {
  const _Host({required this.onSelection});

  final ValueChanged<RecipeSelection> onSelection;

  @override
  State<_Host> createState() => _HostState();
}

class _HostState extends State<_Host> {
  RecipeSelection selection = {};

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: IngredientRecipeField(
            ingredients: _ingredients,
            selection: selection,
            onChanged: (next) {
              setState(() => selection = next);
              widget.onSelection(next);
            },
          ),
        ),
      ),
    );
  }
}

void main() {
  testWidgets('narrows the list as you type', (tester) async {
    await tester.pumpWidget(_Host(onSelection: (_) {}));

    await tester.tap(find.byType(TextField));
    await tester.pump();
    expect(find.byType(CheckboxListTile), findsNWidgets(3));

    await tester.enterText(find.byType(TextField), 'dress');
    await tester.pump();
    expect(find.byType(CheckboxListTile), findsOneWidget);
    expect(find.text('Dressing'), findsOneWidget);
  });

  testWidgets(
    'Burger example: units shown, bun and patty consumed, dressing only checked',
    (tester) async {
      var latest = <String, RecipeEntry>{};
      await tester.pumpWidget(_Host(onSelection: (s) => latest = s));

      await tester.tap(find.byType(TextField));
      await tester.pump();
      await tester.tap(find.text('Burger bun'));
      await tester.pump();
      await tester.tap(find.text('Burger patty'));
      await tester.pump();
      await tester.tap(find.text('Dressing'));
      await tester.pump();

      // A newly chosen ingredient starts as availability-only: no quantity box yet.
      expect(find.textContaining('Quantity per order'), findsNothing);
      expect(find.textContaining('Not deducted when sold'), findsNWidgets(3));

      // Close the list so the chosen cards are on screen, then mark bun and patty as used.
      await tester.tap(find.byTooltip('Hide ingredient list'));
      await tester.pump();
      final usedRadios = find.text('Used up every order');
      await tester.tap(usedRadios.at(0));
      await tester.pump();
      await tester.tap(usedRadios.at(1));
      await tester.pump();

      // The base unit is shown in the label and beside the number.
      expect(find.text('Quantity per order (pair)'), findsOneWidget);
      expect(find.text('Quantity per order (pc)'), findsOneWidget);
      expect(find.text('Quantity per order (mL)'), findsNothing);

      final boxes = find.byType(TextFormField);
      await tester.enterText(boxes.at(0), '1');
      await tester.enterText(boxes.at(1), '1');
      await tester.pump();

      final result = buildRecipeLines(latest);
      expect(result.isOk, isTrue);
      expect(
        {for (final l in result.lines) l.inventoryItemId: l.quantityPerOrder},
        {'bun': 1.0, 'patty': 1.0, 'dressing': null},
      );
    },
  );

  testWidgets('removing a chosen ingredient drops it from the selection', (
    tester,
  ) async {
    var latest = <String, RecipeEntry>{};
    await tester.pumpWidget(_Host(onSelection: (s) => latest = s));
    await tester.tap(find.byType(TextField));
    await tester.pump();
    await tester.tap(find.text('Dressing'));
    await tester.pump();
    expect(latest.keys, ['dressing']);

    await tester.tap(find.byTooltip('Remove Dressing'));
    await tester.pump();
    expect(latest, isEmpty);
  });
}
