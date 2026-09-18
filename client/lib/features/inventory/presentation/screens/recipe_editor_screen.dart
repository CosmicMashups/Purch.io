import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../domain/inventory_item_models.dart';
import '../providers/inventory_item_providers.dart';

/// Editor for an Item's recipe/BOM — which InventoryItems it consumes per
/// order, and how much of each. Only relevant when the tenant has opted into
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
  ConsumerState<RecipeEditorScreen> createState() =>
      _RecipeEditorScreenState();
}

class _RecipeEditorScreenState extends ConsumerState<RecipeEditorScreen> {
  /// inventoryItemId -> quantity text controller. A key existing here means
  /// the checkbox is checked (used in the recipe).
  final Map<String, TextEditingController> _selected = {};
  bool _initialized = false;

  @override
  void dispose() {
    for (final controller in _selected.values) {
      controller.dispose();
    }
    super.dispose();
  }

  void _initializeFromRecipe(List<ItemRecipeLine> lines) {
    if (_initialized) return;
    _initialized = true;
    for (final line in lines) {
      _selected[line.inventoryItemId] = TextEditingController(
        text: line.quantityPerOrder?.toString() ?? '',
      );
    }
  }

  void _toggle(String inventoryItemId, bool checked) {
    setState(() {
      if (checked) {
        _selected[inventoryItemId] = TextEditingController();
      } else {
        _selected.remove(inventoryItemId)?.dispose();
      }
    });
  }

  Future<void> _save() async {
    final lines = [
      for (final entry in _selected.entries)
        ReplaceItemRecipeLineRequest(
          inventoryItemId: entry.key,
          quantityPerOrder: entry.value.text.trim().isEmpty
              ? null
              : double.tryParse(entry.value.text.trim()),
        ),
    ];

    final succeeded = await ref
        .read(replaceItemRecipeControllerProvider.notifier)
        .replace(widget.itemId, ReplaceItemRecipeRequest(lines: lines));

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
    final inventoryItemsAsync = ref.watch(inventoryItemListProvider);
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
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load inventory items: ${describeError(error)}',
          onRetry: () => ref.invalidate(inventoryItemListProvider),
        ),
        data: (inventoryItems) => recipeAsync.when(
          loading: () => const Center(
            child: CircularProgressIndicator(color: AppColors.brandPrimary),
          ),
          error: (error, stackTrace) => ErrorStateView(
            message: 'Could not load recipe: ${describeError(error)}',
            onRetry: () =>
                ref.invalidate(itemRecipeProvider(widget.itemId)),
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
                      style: const TextStyle(color: AppColors.onErrorContainer),
                    ),
                  ),
                Expanded(
                  child: ListView.separated(
                    padding: const EdgeInsets.all(AppSpacing.lg),
                    itemCount: inventoryItems.length,
                    separatorBuilder: (_, __) =>
                        const SizedBox(height: AppSpacing.sm),
                    itemBuilder: (context, index) {
                      final inventoryItem = inventoryItems[index];
                      final controller = _selected[inventoryItem.id];
                      final isChecked = controller != null;

                      return Card(
                        elevation: 0,
                        color: AppColors.surface,
                        shape: RoundedRectangleBorder(
                          borderRadius: AppRadius.mdBorder,
                          side: const BorderSide(color: AppColors.border),
                        ),
                        child: Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: AppSpacing.sm,
                            vertical: AppSpacing.xs,
                          ),
                          child: Column(
                            children: [
                              CheckboxListTile(
                                value: isChecked,
                                onChanged: isSaving
                                    ? null
                                    : (checked) => _toggle(
                                        inventoryItem.id,
                                        checked ?? false,
                                      ),
                                title: Text(inventoryItem.name),
                                subtitle: Text(
                                  '${inventoryItem.baseUnit} / '
                                  '${inventoryItem.packagingUnit}',
                                ),
                                controlAffinity:
                                    ListTileControlAffinity.leading,
                              ),
                              if (isChecked)
                                Padding(
                                  padding: const EdgeInsets.only(
                                    left: AppSpacing.xl,
                                    right: AppSpacing.md,
                                    bottom: AppSpacing.sm,
                                  ),
                                  child: TextField(
                                    controller: controller,
                                    enabled: !isSaving,
                                    keyboardType:
                                        const TextInputType.numberWithOptions(
                                      decimal: true,
                                    ),
                                    decoration: InputDecoration(
                                      labelText:
                                          'Quantity per order (${inventoryItem.baseUnit})',
                                      hintText:
                                          'Leave blank to just check availability',
                                      isDense: true,
                                    ),
                                  ),
                                ),
                            ],
                          ),
                        ),
                      );
                    },
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
                      child: isSaving
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
