import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/routing/auth_gate.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../onboarding/domain/onboarding_enums.dart';
import '../../domain/inventory_item_models.dart';
import '../providers/inventory_item_providers.dart';

/// The groups ingredients are filed under. Anyone in inventory can read them;
/// only an Admin or Manager can add, rename or delete one.
class InventoryCategoryListScreen extends ConsumerWidget {
  const InventoryCategoryListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final categoriesAsync = ref.watch(inventoryCategoryListProvider);
    final ingredients =
        ref.watch(inventoryItemListProvider).valueOrNull ??
        const <InventoryItem>[];
    final role = ref.watch(currentStaffRoleProvider).valueOrNull;
    final canEdit = role == StaffRole.admin || role == StaffRole.manager;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Ingredient Categories'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
      ),
      body: categoriesAsync.when(
        loading:
            () => const Center(
              child: CircularProgressIndicator(color: AppColors.brandPrimary),
            ),
        error:
            (error, _) => ErrorStateView(
              message: 'Could not load categories: ${describeError(error)}',
              onRetry:
                  () => ref.read(inventoryCategoryListProvider.notifier).refresh(),
            ),
        data: (categories) {
          if (categories.isEmpty) {
            return EmptyStateView(
              icon: Icons.category_outlined,
              title: 'No ingredient categories yet.',
              description:
                  canEdit
                      ? 'Group your ingredients, like Dairy or Dry goods.'
                      : 'A manager can add categories.',
              actionLabel: canEdit ? 'Add Category' : null,
              onAction: canEdit ? () => _openForm(context) : null,
            );
          }
          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh:
                () => ref.read(inventoryCategoryListProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.lg),
              itemCount: categories.length,
              separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                final category = categories[index];
                final count =
                    ingredients.where((i) => i.categoryId == category.id).length;
                return Card(
                  elevation: 0,
                  color: AppColors.surface,
                  shape: RoundedRectangleBorder(
                    borderRadius: AppRadius.mdBorder,
                    side: const BorderSide(color: AppColors.border),
                  ),
                  child: ListTile(
                    title: Text(
                      category.name,
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    subtitle: Text(
                      '$count ingredient${count == 1 ? '' : 's'} · position ${category.sortOrder}',
                    ),
                    trailing:
                        canEdit
                            ? Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                IconButton(
                                  tooltip: 'Edit category',
                                  icon: const Icon(Icons.edit_outlined),
                                  onPressed:
                                      () => _openForm(context, existing: category),
                                ),
                                IconButton(
                                  tooltip: 'Delete category',
                                  icon: const Icon(Icons.delete_outline),
                                  onPressed:
                                      () => _confirmDelete(context, ref, category),
                                ),
                              ],
                            )
                            : null,
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton:
          canEdit
              ? FloatingActionButton(
                backgroundColor: AppColors.brandPrimary,
                foregroundColor: Colors.white,
                onPressed: () => _openForm(context),
                tooltip: 'Add category',
                child: const Icon(Icons.add),
              )
              : null,
    );
  }

  void _openForm(BuildContext context, {InventoryCategory? existing}) {
    showDialog<void>(
      context: context,
      builder: (_) => _CategoryFormDialog(existing: existing),
    );
  }

  Future<void> _confirmDelete(
    BuildContext context,
    WidgetRef ref,
    InventoryCategory category,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder:
          (dialogContext) => AlertDialog(
            title: Text('Delete ${category.name}?'),
            content: const Text(
              'Its ingredients are not deleted. They just become uncategorised.',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(false),
                child: const Text('Cancel'),
              ),
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(true),
                child: const Text('Delete'),
              ),
            ],
          ),
    );
    if (confirmed == true) {
      await ref
          .read(inventoryCategoryControllerProvider.notifier)
          .delete(category.id);
    }
  }
}

class _CategoryFormDialog extends ConsumerStatefulWidget {
  const _CategoryFormDialog({this.existing});

  final InventoryCategory? existing;

  @override
  ConsumerState<_CategoryFormDialog> createState() =>
      _CategoryFormDialogState();
}

class _CategoryFormDialogState extends ConsumerState<_CategoryFormDialog> {
  final _formKey = GlobalKey<FormState>();
  late final _nameController = TextEditingController(
    text: widget.existing?.name,
  );
  late final _sortOrderController = TextEditingController(
    text: (widget.existing?.sortOrder ?? 0).toString(),
  );

  @override
  void dispose() {
    _nameController.dispose();
    _sortOrderController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    final request = InventoryCategoryRequest(
      name: _nameController.text.trim(),
      sortOrder: int.parse(_sortOrderController.text.trim()),
    );
    final controller = ref.read(inventoryCategoryControllerProvider.notifier);
    final succeeded =
        widget.existing == null
            ? await controller.create(request)
            : await controller.updateCategory(widget.existing!.id, request);
    if (mounted && succeeded) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(inventoryCategoryControllerProvider);
    final failure =
        ref.read(inventoryCategoryControllerProvider.notifier).currentFailure;

    return AlertDialog(
      title: Text(
        widget.existing == null ? 'New Category' : 'Edit Category',
      ),
      content: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextFormField(
              controller: _nameController,
              enabled: !state.isLoading,
              decoration: const InputDecoration(labelText: 'Name'),
              validator:
                  (value) =>
                      (value == null || value.trim().isEmpty)
                          ? 'Required'
                          : null,
            ),
            const SizedBox(height: AppSpacing.sm),
            TextFormField(
              controller: _sortOrderController,
              enabled: !state.isLoading,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(
                labelText: 'Position',
                helperText: 'Lower numbers come first',
              ),
              validator:
                  (value) =>
                      int.tryParse(value?.trim() ?? '') == null
                          ? 'Enter a whole number'
                          : null,
            ),
            if (failure != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text(
                failure.message,
                style: const TextStyle(color: AppColors.error),
              ),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: state.isLoading ? null : () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: state.isLoading ? null : _submit,
          child: Text(widget.existing == null ? 'Add' : 'Save'),
        ),
      ],
    );
  }
}
