import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/inventory/domain/inventory_item_models.dart';
import 'package:purch_client/features/inventory/domain/recipe_selection.dart';

void main() {
  group('selectionFromRecipe', () {
    test('marks a line with a quantity as used and a blank one as checked only', () {
      final selection = selectionFromRecipe(const [
        ItemRecipeLine(
          inventoryItemId: 'bun',
          inventoryItemName: 'Burger bun',
          quantityPerOrder: 1,
        ),
        ItemRecipeLine(
          inventoryItemId: 'dressing',
          inventoryItemName: 'Dressing',
          quantityPerOrder: null,
        ),
      ]);

      expect(selection['bun'], const RecipeEntry(used: true, quantity: '1'));
      expect(selection['dressing'], const RecipeEntry());
    });
  });

  group('buildRecipeLines', () {
    test('Burger example: bun and patty are consumed, dressing is not', () {
      final result = buildRecipeLines({
        'bun': const RecipeEntry(used: true, quantity: '1'),
        'patty': const RecipeEntry(used: true, quantity: '1'),
        'dressing': const RecipeEntry(),
      });

      expect(result.isOk, isTrue);
      expect(result.lines.map((l) => l.quantityPerOrder), [1, 1, null]);
    });

    test('ignores what was typed once an ingredient is availability-only', () {
      final result = buildRecipeLines({
        'a': const RecipeEntry(used: false, quantity: '999'),
      });
      expect(result.lines.single.quantityPerOrder, isNull);
    });

    test('an empty recipe is allowed', () {
      expect(buildRecipeLines({}).isOk, isTrue);
    });

    test('a used ingredient needs a quantity above zero', () {
      String? message(String quantity) =>
          buildRecipeLines({
            'a': RecipeEntry(used: true, quantity: quantity),
          }).error?.message;

      expect(message('  '), contains('Enter how much'));
      expect(message('abc'), 'Enter a number');
      expect(message('-1'), 'Cannot be negative');
      expect(message('0'), 'Must be more than 0');
      expect(message('18.5'), isNull);
    });
  });

  group('sameRecipeSelection', () {
    test('ignores a typed quantity on an availability-only ingredient', () {
      expect(
        sameRecipeSelection(
          {'a': const RecipeEntry(used: false, quantity: '5')},
          {'a': const RecipeEntry()},
        ),
        isTrue,
      );
    });

    test('notices a different ingredient, mode or quantity', () {
      const used = RecipeEntry(used: true, quantity: '1');
      expect(sameRecipeSelection({'a': used}, {'b': used}), isFalse);
      expect(sameRecipeSelection({'a': used}, {'a': const RecipeEntry()}), isFalse);
      expect(
        sameRecipeSelection(
          {'a': used},
          {'a': const RecipeEntry(used: true, quantity: '2')},
        ),
        isFalse,
      );
    });
  });
}
