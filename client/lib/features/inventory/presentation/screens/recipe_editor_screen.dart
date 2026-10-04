import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../domain/inventory_item_models.dart';
import '../../domain/recipe_selection.dart';
import '../providers/inventory_item_providers.dart';
import '../widgets/ingredient_recipe_field.dart';

/// Editor for an Item's recipe/BOM — which InventoryItems it uses, and for each
/// whether it is used up on every order (and how much) or only checked for
/// availability. Only relevant when the tenant has opted into
/// useSeparateInventoryTracking.
class RecipeEditorScreen extends ConsumerStatefulWidget {
  const RecipeEditorScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  ConsumerState<RecipeEditorScreen> createState() => _RecipeEditorScreenState();
}

class _RecipeEditorScreenState extends ConsumerState<RecipeEditorScreen> {
  RecipeSelection _selection = {};
  RecipeLineError? _lineError;
  bool _initialized = false;

  void _initializeFromRecipe(List<ItemRecipeLine> lines) {
    if (_initialized) return;
    _initialized = true;
    _selection = selectionFromRecipe(lines);
  }

  Future<void> _save() async {
    final result = buildRecipeLines(_selection);
    if (!result.isOk) {
      setState(() => _lineError = result.error);
      return;
    }
    setState(() => _lineError = null);

    final succeeded = await ref
        .read(replaceItemRecipeControllerProvider.notifier)
        .replace(widget.itemId, ReplaceItemRecipeRequest(lines: result.lines));

    if (!mounted) return;
    if (succeeded) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: AppColors.success,
          content: Text('Recipe for "${widget.itemName}" saved'),
        ),
      );
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    // An item's own paired stock record can't be an ingredient of its own recipe.
    final inventoryItemsAsync = ref
        .watch(inventoryItemListProvider)
        .whenData(
          (all) => all.where((i) => i.linkedItemId != widget.itemId).toList(),
        );
    final recipeAsync = ref.watch(itemRecipeProvider(widget.itemId));
    final isSaving = ref.watch(replaceItemRecipeControllerProvider).isLoading;
    final failure =
        ref.read(replaceItemRecipeControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Recipe — ${widget.itemName}'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
      ),
      body: inventoryItemsAsync.when(
        loading:
            () => const Center(
              child: CircularProgressIndicator(color: AppColors.brandPrimary),
            ),
        error:
            (error, stackTrace) => ErrorStateView(
              message: 'Could not load inventory items: ${describeError(error)}',
              onRetry: () => ref.invalidate(inventoryItemListProvider),
            ),
        data:
            (inventoryItems) => recipeAsync.when(
              loading:
                  () => const Center(
                    child: CircularProgressIndicator(
                      color: AppColors.brandPrimary,
                    ),
                  ),
              error:
                  (error, stackTrace) => ErrorStateView(
                    message: 'Could not load recipe: ${describeError(error)}',
                    onRetry:
                        () => ref.invalidate(itemRecipeProvider(widget.itemId)),
                  ),
              data: (lines) {
                _initializeFromRecipe(lines);

                if (inventoryItems.isEmpty) {
                  return const Center(
                    child: Padding(
                      padding: EdgeInsets.all(AppSpacing.lg),
                      child: Text(
                        'No inventory items yet. Add some from the Inventory '
                        'Items screen before building a recipe.',
                        textAlign: TextAlign.center,
                        style: TextStyle(color: AppColors.textSecondary),
                      ),
                    ),
                  );
                }

                return Column(
                  children: [
                    if (failure != null)
                      Container(
                        width: double.infinity,
                        padding: const EdgeInsets.all(AppSpacing.md),
                        color: AppColors.errorContainer,
                        child: Text(
                          failure.message,
                          style: const TextStyle(
                            color: AppColors.onErrorContainer,
                          ),
                        ),
                      ),
                    Expanded(
                      child: ListView(
                        padding: const EdgeInsets.all(AppSpacing.lg),
                        children: [
                          const Text(
                            'An item is either its own inventory item or made '
                            'from a recipe — never both. Saving a recipe '
                            'retires this item\'s own stock record (its stock '
                            'must be zero first). Save an empty recipe to make '
                            'it an inventory item again.',
                            style: TextStyle(color: AppColors.textSecondary),
                          ),
                          const SizedBox(height: AppSpacing.lg),
                          IngredientRecipeField(
                            ingredients: inventoryItems,
                            selection: _selection,
                            lineError: _lineError,
                            enabled: !isSaving,
                            onChanged:
                                (next) => setState(() {
                                  _lineError = null;
                                  _selection = next;
                                }),
                          ),
                        ],
                      ),
                    ),
                    Padding(
                      padding: const EdgeInsets.all(AppSpacing.lg),
                      child: SizedBox(
                        width: double.infinity,
                        height: 48,
                        child: FilledButton(
                          onPressed: isSaving ? null : _save,
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            foregroundColor: AppColors.onBrandPrimary,
                            shape: const RoundedRectangleBorder(
                              borderRadius: AppRadius.mdBorder,
                            ),
                          ),
                          child:
                              isSaving
                                  ? const SizedBox(
                                    height: 20,
                                    width: 20,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2,
                                      color: Colors.white,
                                    ),
                                  )
                                  : const Text(
                                    'Save Recipe',
                                    style: TextStyle(
                                      fontSize: 16,
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                        ),
                      ),
                    ),
                  ],
                );
              },
            ),
      ),
    );
  }
}
