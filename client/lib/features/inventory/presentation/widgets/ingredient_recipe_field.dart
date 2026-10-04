import 'package:flutter/material.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/inventory_item_models.dart';
import '../../domain/recipe_selection.dart';

/// Pick the ingredients an item is made from: type to narrow the list, tick the
/// ones you need, then say for each whether it is used up on every order (with
/// how much, in its own base unit) or only checked for availability.
class IngredientRecipeField extends StatefulWidget {
  const IngredientRecipeField({
    super.key,
    required this.ingredients,
    required this.selection,
    required this.onChanged,
    this.lineError,
    this.enabled = true,
  });

  /// Everything that can be picked.
  final List<InventoryItem> ingredients;
  final RecipeSelection selection;
  final ValueChanged<RecipeSelection> onChanged;
  final RecipeLineError? lineError;
  final bool enabled;

  @override
  State<IngredientRecipeField> createState() => _IngredientRecipeFieldState();
}

class _IngredientRecipeFieldState extends State<IngredientRecipeField> {
  final _searchController = TextEditingController();
  final _focusNode = FocusNode();
  bool _open = false;

  @override
  void initState() {
    super.initState();
    _focusNode.addListener(() {
      if (_focusNode.hasFocus) setState(() => _open = true);
    });
  }

  @override
  void dispose() {
    _searchController.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  void _toggle(String id) {
    final next = Map<String, RecipeEntry>.of(widget.selection);
    if (next.containsKey(id)) {
      next.remove(id);
    } else {
      // New ingredients start as availability-only, so nothing is deducted
      // until someone says how much.
      next[id] = const RecipeEntry();
    }
    widget.onChanged(next);
  }

  void _update(String id, RecipeEntry entry) {
    widget.onChanged({...widget.selection, id: entry});
  }

  List<InventoryItem> get _matches {
    final needle = _searchController.text.trim().toLowerCase();
    if (needle.isEmpty) return widget.ingredients;
    return widget.ingredients
        .where(
          (i) =>
              i.name.toLowerCase().contains(needle) ||
              (i.sku ?? '').toLowerCase().contains(needle),
        )
        .toList();
  }

  @override
  Widget build(BuildContext context) {
    final chosen =
        widget.ingredients
            .where((i) => widget.selection.containsKey(i.id))
            .toList();
    final matches = _matches;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextField(
          controller: _searchController,
          focusNode: _focusNode,
          enabled: widget.enabled,
          decoration: InputDecoration(
            labelText: 'Ingredients',
            hintText:
                chosen.isEmpty
                    ? 'Search ingredients'
                    : '${chosen.length} selected. Search to add more',
            prefixIcon: const Icon(Icons.search),
            suffixIcon: IconButton(
              tooltip: _open ? 'Hide ingredient list' : 'Show ingredient list',
              icon: Icon(_open ? Icons.arrow_drop_up : Icons.arrow_drop_down),
              onPressed: () => setState(() => _open = !_open),
            ),
            border: OutlineInputBorder(borderRadius: AppRadius.smBorder),
          ),
          onChanged: (_) => setState(() => _open = true),
        ),
        if (_open)
          Container(
            margin: const EdgeInsets.only(top: AppSpacing.xs),
            constraints: const BoxConstraints(maxHeight: 240),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: AppRadius.smBorder,
              border: Border.all(color: AppColors.border),
            ),
            child:
                matches.isEmpty
                    ? const Padding(
                      padding: EdgeInsets.all(AppSpacing.md),
                      child: Text('No ingredient matches'),
                    )
                    : ListView(
                      shrinkWrap: true,
                      children: [
                        for (final ingredient in matches)
                          CheckboxListTile(
                            dense: true,
                            value: widget.selection.containsKey(ingredient.id),
                            onChanged:
                                widget.enabled
                                    ? (_) => _toggle(ingredient.id)
                                    : null,
                            controlAffinity: ListTileControlAffinity.leading,
                            title: Text(ingredient.name),
                            secondary: Text(ingredient.baseUnit),
                          ),
                      ],
                    ),
          ),
        for (final ingredient in chosen)
          _ChosenIngredient(
            key: ValueKey(ingredient.id),
            ingredient: ingredient,
            entry: widget.selection[ingredient.id]!,
            enabled: widget.enabled,
            error:
                widget.lineError?.inventoryItemId == ingredient.id
                    ? widget.lineError!.message
                    : null,
            onChanged: (entry) => _update(ingredient.id, entry),
            onRemove: () => _toggle(ingredient.id),
          ),
      ],
    );
  }
}

class _ChosenIngredient extends StatelessWidget {
  const _ChosenIngredient({
    super.key,
    required this.ingredient,
    required this.entry,
    required this.enabled,
    required this.error,
    required this.onChanged,
    required this.onRemove,
  });

  final InventoryItem ingredient;
  final RecipeEntry entry;
  final bool enabled;
  final String? error;
  final ValueChanged<RecipeEntry> onChanged;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(top: AppSpacing.sm),
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  ingredient.name,
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
              ),
              IconButton(
                tooltip: 'Remove ${ingredient.name}',
                icon: const Icon(Icons.close),
                onPressed: enabled ? onRemove : null,
              ),
            ],
          ),
          RadioListTile<bool>(
            dense: true,
            contentPadding: EdgeInsets.zero,
            value: true,
            groupValue: entry.used,
            onChanged:
                enabled ? (_) => onChanged(entry.copyWith(used: true)) : null,
            title: const Text('Used up every order'),
          ),
          RadioListTile<bool>(
            dense: true,
            contentPadding: EdgeInsets.zero,
            value: false,
            groupValue: entry.used,
            onChanged:
                enabled ? (_) => onChanged(entry.copyWith(used: false)) : null,
            title: const Text("Just check it's in stock"),
          ),
          if (entry.used) ...[
            const SizedBox(height: AppSpacing.xs),
            TextFormField(
              key: ValueKey('${ingredient.id}-quantity'),
              initialValue: entry.quantity,
              enabled: enabled,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(
                labelText: 'Quantity per order (${ingredient.baseUnit})',
                // The unit sits beside the number, so nobody has to guess it.
                suffixText: ingredient.baseUnit,
                helperText: 'Taken from stock every time this item is sold.',
                errorText: error,
                isDense: true,
              ),
              onChanged: (text) => onChanged(entry.copyWith(quantity: text)),
            ),
          ] else
            const Padding(
              padding: EdgeInsets.only(top: AppSpacing.xs),
              child: Text(
                'Not deducted when sold. The item shows as out of stock only '
                'when this reaches 0, so set its count with Count stock, for '
                'example at the end of a shift.',
                style: TextStyle(color: AppColors.textSecondary, fontSize: 12),
              ),
            ),
        ],
      ),
    );
  }
}
