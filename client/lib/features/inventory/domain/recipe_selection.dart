import 'inventory_item_models.dart';

/// How one ingredient is used by an item. Used: [quantity] is taken from stock
/// on every sale. Not used: the ingredient is only checked for availability (a
/// sauce, say), so its stock changes only when someone counts it.
class RecipeEntry {
  const RecipeEntry({this.used = false, this.quantity = ''});

  final bool used;

  /// The quantity text as typed. Only read when [used] is true.
  final String quantity;

  RecipeEntry copyWith({bool? used, String? quantity}) =>
      RecipeEntry(used: used ?? this.used, quantity: quantity ?? this.quantity);

  @override
  bool operator ==(Object other) =>
      other is RecipeEntry && other.used == used && other.quantity == quantity;

  @override
  int get hashCode => Object.hash(used, quantity);
}

/// inventoryItemId -> how it is used. A key being present means it is chosen.
typedef RecipeSelection = Map<String, RecipeEntry>;

RecipeSelection selectionFromRecipe(List<ItemRecipeLine> lines) {
  return {
    for (final line in lines)
      line.inventoryItemId:
          line.quantityPerOrder == null
              ? const RecipeEntry()
              : RecipeEntry(
                used: true,
                quantity: _formatQuantity(line.quantityPerOrder!),
              ),
  };
}

String _formatQuantity(double value) =>
    value == value.truncateToDouble() ? value.toInt().toString() : '$value';

/// A problem with one chosen ingredient's quantity.
class RecipeLineError {
  const RecipeLineError(this.inventoryItemId, this.message);

  final String inventoryItemId;
  final String message;
}

/// Either the lines to send or the first problem found.
class RecipeBuildResult {
  const RecipeBuildResult.ok(this.lines) : error = null;
  const RecipeBuildResult.failed(this.error) : lines = const [];

  final List<ReplaceItemRecipeLineRequest> lines;
  final RecipeLineError? error;

  bool get isOk => error == null;
}

/// An ingredient that is used up must say how much, and more than nothing. One
/// that is only checked has no quantity (null). The API applies its own rules
/// too and still has the final say.
RecipeBuildResult buildRecipeLines(RecipeSelection selection) {
  final lines = <ReplaceItemRecipeLineRequest>[];
  for (final entry in selection.entries) {
    final id = entry.key;
    if (!entry.value.used) {
      lines.add(ReplaceItemRecipeLineRequest(inventoryItemId: id));
      continue;
    }
    final text = entry.value.quantity.trim();
    if (text.isEmpty) {
      return RecipeBuildResult.failed(
        RecipeLineError(
          id,
          "Enter how much is used per order, or choose Just check it's in stock",
        ),
      );
    }
    final quantity = double.tryParse(text);
    if (quantity == null) {
      return RecipeBuildResult.failed(RecipeLineError(id, 'Enter a number'));
    }
    if (quantity < 0) {
      return RecipeBuildResult.failed(
        RecipeLineError(id, 'Cannot be negative'),
      );
    }
    if (quantity == 0) {
      return RecipeBuildResult.failed(
        RecipeLineError(id, 'Must be more than 0'),
      );
    }
    lines.add(
      ReplaceItemRecipeLineRequest(
        inventoryItemId: id,
        quantityPerOrder: quantity,
      ),
    );
  }
  return RecipeBuildResult.ok(lines);
}

/// Whether two selections would save the same recipe.
bool sameRecipeSelection(RecipeSelection a, RecipeSelection b) {
  if (a.length != b.length) return false;
  for (final entry in a.entries) {
    final other = b[entry.key];
    if (other == null) return false;
    if (entry.value.used != other.used) return false;
    if (entry.value.used && entry.value.quantity.trim() != other.quantity.trim()) {
      return false;
    }
  }
  return true;
}
