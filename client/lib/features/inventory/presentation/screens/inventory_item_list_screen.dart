import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../../core/widgets/status_badge.dart';
import '../../../onboarding/domain/branch_models.dart';
import '../../../onboarding/presentation/providers/branch_scope_providers.dart';
import '../../domain/inventory_item_models.dart';
import '../providers/inventory_item_providers.dart';

const _baseUnits = ['pc', 'g', 'kg', 'mL', 'L'];

/// The dropdown entry that lets someone type any other unit, like pair or tray.
const _otherUnit = 'Other…';

/// Ingredient-level inventory — opt-in per tenant via
/// useSeparateInventoryTracking. Lists InventoryItems, lets the user add new
/// ones, and record physical counts / stock receipts against them.
class InventoryItemListScreen extends ConsumerStatefulWidget {
  const InventoryItemListScreen({super.key, this.receiveItemId});

  /// Set by "Restock first": opens that ingredient's delivery form once the
  /// list has loaded.
  final String? receiveItemId;

  @override
  ConsumerState<InventoryItemListScreen> createState() =>
      _InventoryItemListScreenState();
}

class _InventoryItemListScreenState
    extends ConsumerState<InventoryItemListScreen> {
  /// null shows everything, '' only the uncategorised, otherwise a category id.
  String? _filter;
  bool _receiveOpened = false;

  String _formatQuantity(InventoryItem item) {
    if (item.packagingSize != 1) {
      final whole = item.wholePackagesRemaining;
      final partial = item.partialPackageRemainder;
      final partialText = partial % 1 == 0
          ? partial.toInt().toString()
          : partial.toStringAsFixed(1);
      return '$whole ${item.packagingUnit} + $partialText ${item.baseUnit}';
    }
    final formatted = item.quantityOnHand % 1 == 0
        ? item.quantityOnHand.toInt().toString()
        : item.quantityOnHand.toStringAsFixed(1);
    return '$formatted ${item.baseUnit}';
  }

  @override
  Widget build(BuildContext context) {
    final itemsAsync = ref.watch(inventoryItemListProvider);
    final categories =
        ref.watch(inventoryCategoryListProvider).valueOrNull ??
        const <InventoryCategory>[];
    final categoryNames = {for (final c in categories) c.id: c.name};

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Inventory Items'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
        actions: [
          TextButton.icon(
            onPressed: () => context.push('/inventory/inventory-categories'),
            icon: const Icon(Icons.category_outlined),
            label: const Text('Categories'),
          ),
        ],
      ),
      body: itemsAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load inventory items: ${describeError(error)}',
          onRetry: () => ref.read(inventoryItemListProvider.notifier).refresh(),
        ),
        data: (items) {
          if (items.isEmpty) {
            return EmptyStateView(
              icon: Icons.egg_outlined,
              title: 'No inventory items yet — tap + to add one.',
              description:
                  'Track ingredient-level stock (e.g. coffee beans, soy sauce) '
                  'separately from what\'s sold at the register.',
              actionLabel: 'Add Inventory Item',
              onAction: () => _openCreateDialog(context, ref),
            );
          }

          final receiveId = widget.receiveItemId;
          if (receiveId != null && !_receiveOpened) {
            _receiveOpened = true;
            final target = items.where((i) => i.id == receiveId).firstOrNull;
            if (target != null) {
              WidgetsBinding.instance.addPostFrameCallback((_) {
                if (mounted) _openReceiveStockDialog(context, ref, target);
              });
            }
          }

          final shown =
              _filter == null
                  ? items
                  : items
                      .where((i) => (i.categoryId ?? '') == _filter)
                      .toList();

          final list = RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh: () =>
                ref.read(inventoryItemListProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.lg),
              itemCount: shown.length,
              separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                final item = shown[index];
                return Card(
                  elevation: 0,
                  color: AppColors.surface,
                  shape: RoundedRectangleBorder(
                    borderRadius: AppRadius.mdBorder,
                    side: const BorderSide(color: AppColors.border),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.xs,
                    ),
                    onTap: () => _openActionsSheet(context, ref, item),
                    leading: Container(
                      padding: const EdgeInsets.all(AppSpacing.sm),
                      decoration: BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        borderRadius: AppRadius.mdBorder,
                      ),
                      child: const Icon(
                        Icons.egg_outlined,
                        color: AppColors.brandPrimary,
                        size: 22,
                      ),
                    ),
                    title: Text(
                      item.name,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: Padding(
                      padding: const EdgeInsets.only(top: 2),
                      child: Text(
                        '${_formatQuantity(item)}'
                        '${categoryNames[item.categoryId] != null ? ' · ${categoryNames[item.categoryId]}' : ''}'
                        '${item.sku != null ? ' · SKU ${item.sku}' : ''}'
                        '${item.isCountedByHand ? '\nCounted by hand: not deducted when items sell. Use Physical count, e.g. at the end of a shift.' : ''}',
                        style: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 13,
                        ),
                      ),
                    ),
                    trailing: StatusBadge.stockLevel(
                      stockOnHand: item.quantityOnHand,
                      lowStockThreshold: item.lowStockThreshold,
                      isSmall: true,
                    ),
                  ),
                );
              },
            ),
          );

          if (categories.isEmpty) return list;
          return Column(
            children: [
              SizedBox(
                height: 56,
                child: ListView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(
                    horizontal: AppSpacing.lg,
                    vertical: AppSpacing.sm,
                  ),
                  children: [
                    for (final chip in <(String?, String)>[
                      (null, 'All'),
                      for (final c in categories) (c.id, c.name),
                      ('', 'Uncategorised'),
                    ])
                      Padding(
                        padding: const EdgeInsets.only(right: AppSpacing.sm),
                        child: ChoiceChip(
                          label: Text(chip.$2),
                          selected: _filter == chip.$1,
                          onSelected: (_) => setState(() => _filter = chip.$1),
                        ),
                      ),
                  ],
                ),
              ),
              Expanded(child: list),
            ],
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: Colors.white,
        onPressed: () => _openCreateDialog(context, ref),
        tooltip: 'Add inventory item',
        child: const Icon(Icons.add),
      ),
    );
  }

  void _openActionsSheet(
    BuildContext context,
    WidgetRef ref,
    InventoryItem item,
  ) {
    showModalBottomSheet<void>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.edit_outlined),
              title: const Text('Edit item'),
              onTap: () {
                Navigator.of(sheetContext).pop();
                _openEditDialog(context, ref, item);
              },
            ),
            ListTile(
              leading: const Icon(Icons.fact_check_outlined),
              title: const Text('Physical count'),
              onTap: () {
                Navigator.of(sheetContext).pop();
                _openPhysicalCountDialog(context, ref, item);
              },
            ),
            ListTile(
              leading: const Icon(Icons.move_to_inbox_outlined),
              title: const Text('Receive stock'),
              onTap: () {
                Navigator.of(sheetContext).pop();
                _openReceiveStockDialog(context, ref, item);
              },
            ),
          ],
        ),
      ),
    );
  }

  void _openCreateDialog(BuildContext context, WidgetRef ref) {
    showDialog<void>(
      context: context,
      builder: (_) => const _InventoryItemFormDialog(),
    );
  }

  void _openEditDialog(BuildContext context, WidgetRef ref, InventoryItem item) {
    showDialog<void>(
      context: context,
      builder: (_) => _InventoryItemFormDialog(existing: item),
    );
  }

  void _openPhysicalCountDialog(
    BuildContext context,
    WidgetRef ref,
    InventoryItem item,
  ) {
    showDialog<void>(
      context: context,
      builder: (_) => _PhysicalCountDialog(item: item),
    );
  }

  void _openReceiveStockDialog(
    BuildContext context,
    WidgetRef ref,
    InventoryItem item,
  ) {
    showDialog<void>(
      context: context,
      builder: (_) => _ReceiveStockDialog(item: item),
    );
  }
}

class _InventoryItemFormDialog extends ConsumerStatefulWidget {
  const _InventoryItemFormDialog({this.existing});

  final InventoryItem? existing;

  @override
  ConsumerState<_InventoryItemFormDialog> createState() =>
      _InventoryItemFormDialogState();
}

class _InventoryItemFormDialogState
    extends ConsumerState<_InventoryItemFormDialog> {
  final _formKey = GlobalKey<FormState>();
  late final _nameController = TextEditingController(
    text: widget.existing?.name,
  );
  late final _skuController = TextEditingController(
    text: widget.existing?.sku,
  );
  late final _packagingUnitController = TextEditingController(
    text: widget.existing?.packagingUnit,
  );
  late final _packagingSizeController = TextEditingController(
    text: widget.existing?.packagingSize.toString(),
  );
  late final _lowStockThresholdController = TextEditingController(
    text: widget.existing?.lowStockThreshold?.toString(),
  );
  // A unit outside the common list (like pair, made on the web) opens as Other with its name filled in.
  late String _baseUnit =
      widget.existing == null || _baseUnits.contains(widget.existing!.baseUnit)
          ? (widget.existing?.baseUnit ?? _baseUnits.first)
          : _otherUnit;
  late final _customUnitController = TextEditingController(
    text:
        widget.existing != null && !_baseUnits.contains(widget.existing!.baseUnit)
            ? widget.existing!.baseUnit
            : '',
  );
  late bool _isActive = widget.existing?.isActive ?? true;
  late String? _categoryId = widget.existing?.categoryId;

  @override
  void dispose() {
    _nameController.dispose();
    _skuController.dispose();
    _packagingUnitController.dispose();
    _packagingSizeController.dispose();
    _lowStockThresholdController.dispose();
    _customUnitController.dispose();
    super.dispose();
  }

  bool get _isEditing => widget.existing != null;

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final packagingSize = double.parse(_packagingSizeController.text.trim());
    final lowStockThreshold = _lowStockThresholdController.text.trim().isEmpty
        ? null
        : double.tryParse(_lowStockThresholdController.text.trim());
    final sku =
        _skuController.text.trim().isEmpty ? null : _skuController.text.trim();
    final baseUnit =
        _baseUnit == _otherUnit ? _customUnitController.text.trim() : _baseUnit;

    bool succeeded;
    if (_isEditing) {
      succeeded = await ref
          .read(updateInventoryItemControllerProvider.notifier)
          .updateItem(
            widget.existing!.id,
            UpdateInventoryItemRequest(
              name: _nameController.text.trim(),
              sku: sku,
              baseUnit: baseUnit,
              packagingUnit: _packagingUnitController.text.trim(),
              packagingSize: packagingSize,
              lowStockThreshold: lowStockThreshold,
              isActive: _isActive,
              categoryId: _categoryId,
            ),
          );
    } else {
      succeeded = await ref
          .read(createInventoryItemControllerProvider.notifier)
          .create(
            CreateInventoryItemRequest(
              name: _nameController.text.trim(),
              sku: sku,
              baseUnit: baseUnit,
              packagingUnit: _packagingUnitController.text.trim(),
              packagingSize: packagingSize,
              lowStockThreshold: lowStockThreshold,
              categoryId: _categoryId,
            ),
          );
    }

    if (!mounted) return;
    if (succeeded) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final isLoading = _isEditing
        ? ref.watch(updateInventoryItemControllerProvider).isLoading
        : ref.watch(createInventoryItemControllerProvider).isLoading;
    final failure = _isEditing
        ? ref.read(updateInventoryItemControllerProvider.notifier).currentFailure
        : ref.read(createInventoryItemControllerProvider.notifier).currentFailure;

    return AlertDialog(
      title: Text(_isEditing ? 'Edit Inventory Item' : 'New Inventory Item'),
      content: SingleChildScrollView(
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextFormField(
                controller: _nameController,
                enabled: !isLoading,
                decoration: const InputDecoration(labelText: 'Name'),
                validator: (value) => (value == null || value.trim().isEmpty)
                    ? 'Required'
                    : null,
              ),
              const SizedBox(height: AppSpacing.sm),
              TextFormField(
                controller: _skuController,
                enabled: !isLoading,
                decoration: const InputDecoration(labelText: 'SKU (optional)'),
              ),
              const SizedBox(height: AppSpacing.sm),
              DropdownButtonFormField<String>(
                value: _baseUnit,
                decoration: const InputDecoration(labelText: 'Base unit'),
                items: [
                  for (final unit in [..._baseUnits, _otherUnit])
                    DropdownMenuItem(value: unit, child: Text(unit)),
                ],
                onChanged: isLoading
                    ? null
                    : (value) => setState(() => _baseUnit = value ?? _baseUnit),
              ),
              if (_baseUnit == _otherUnit) ...[
                const SizedBox(height: AppSpacing.sm),
                TextFormField(
                  controller: _customUnitController,
                  enabled: !isLoading,
                  decoration: const InputDecoration(
                    labelText: 'Unit name',
                    hintText: 'e.g. pair, tray, bottle',
                  ),
                  validator: (value) =>
                      _baseUnit == _otherUnit &&
                              (value == null || value.trim().isEmpty)
                          ? 'Required'
                          : null,
                ),
              ],
              const SizedBox(height: AppSpacing.sm),
              TextFormField(
                controller: _packagingUnitController,
                enabled: !isLoading,
                decoration: const InputDecoration(
                  labelText: 'Packaging unit',
                  hintText: 'e.g. case, sack, box',
                ),
                validator: (value) => (value == null || value.trim().isEmpty)
                    ? 'Required'
                    : null,
              ),
              const SizedBox(height: AppSpacing.sm),
              TextFormField(
                controller: _packagingSizeController,
                enabled: !isLoading,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: const InputDecoration(
                  labelText: 'Packaging size',
                  hintText: 'Base units per packaging unit',
                ),
                validator: (value) {
                  final parsed = double.tryParse(value?.trim() ?? '');
                  if (parsed == null || parsed <= 0) {
                    return 'Enter a positive number';
                  }
                  return null;
                },
              ),
              const SizedBox(height: AppSpacing.sm),
              DropdownButtonFormField<String?>(
                value: _categoryId,
                decoration: const InputDecoration(
                  labelText: 'Category (optional)',
                ),
                items: [
                  const DropdownMenuItem<String?>(
                    value: null,
                    child: Text('Uncategorised'),
                  ),
                  for (final c
                      in ref.watch(inventoryCategoryListProvider).valueOrNull ??
                          const <InventoryCategory>[])
                    DropdownMenuItem<String?>(value: c.id, child: Text(c.name)),
                ],
                onChanged:
                    isLoading ? null : (value) => setState(() => _categoryId = value),
              ),
              const SizedBox(height: AppSpacing.sm),
              TextFormField(
                controller: _lowStockThresholdController,
                enabled: !isLoading,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: const InputDecoration(
                  labelText: 'Low stock threshold (optional)',
                ),
              ),
              if (_isEditing) ...[
                const SizedBox(height: AppSpacing.sm),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Active'),
                  value: _isActive,
                  onChanged: isLoading
                      ? null
                      : (value) => setState(() => _isActive = value),
                ),
              ],
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
      ),
      actions: [
        TextButton(
          onPressed: isLoading ? null : () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: isLoading ? null : _submit,
          style: FilledButton.styleFrom(backgroundColor: AppColors.brandPrimary),
          child: isLoading
              ? const SizedBox(
                  height: 18,
                  width: 18,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: Colors.white,
                  ),
                )
              : Text(_isEditing ? 'Save' : 'Create'),
        ),
      ],
    );
  }
}

class _PhysicalCountDialog extends ConsumerStatefulWidget {
  const _PhysicalCountDialog({required this.item});

  final InventoryItem item;

  @override
  ConsumerState<_PhysicalCountDialog> createState() =>
      _PhysicalCountDialogState();
}

class _PhysicalCountDialogState extends ConsumerState<_PhysicalCountDialog> {
  final _formKey = GlobalKey<FormState>();
  late final _quantityController = TextEditingController(
    text: widget.item.quantityOnHand.toString(),
  );
  Branch? _selectedBranch;

  @override
  void dispose() {
    _quantityController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false) ||
        _selectedBranch == null) {
      return;
    }

    final succeeded = await ref
        .read(physicalCountControllerProvider.notifier)
        .submit(
          widget.item.id,
          UpdatePhysicalCountRequest(
            quantityOnHand: double.parse(_quantityController.text.trim()),
            branchId: _selectedBranch!.id,
          ),
        );

    if (!mounted) return;
    if (succeeded) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final isLoading = ref.watch(physicalCountControllerProvider).isLoading;
    final failure =
        ref.read(physicalCountControllerProvider.notifier).currentFailure;
    final branchesAsync = ref.watch(selectableBranchesProvider);

    return AlertDialog(
      title: Text('Physical Count — ${widget.item.name}'),
      content: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            branchesAsync.when(
              loading: () => const LinearProgressIndicator(
                color: AppColors.brandPrimary,
              ),
              error: (error, stackTrace) => Text(
                'Could not load branches: ${describeError(error)}',
                style: const TextStyle(color: AppColors.error),
              ),
              data: (branches) => DropdownButtonFormField<Branch>(
                value: _selectedBranch,
                decoration: const InputDecoration(labelText: 'Branch'),
                items: [
                  for (final branch in branches)
                    DropdownMenuItem(value: branch, child: Text(branch.name)),
                ],
                onChanged: isLoading
                    ? null
                    : (branch) => setState(() => _selectedBranch = branch),
                validator: (value) => value == null ? 'Choose a branch' : null,
              ),
            ),
            const SizedBox(height: AppSpacing.sm),
            TextFormField(
              controller: _quantityController,
              enabled: !isLoading,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: const InputDecoration(
                labelText: 'Counted quantity on hand',
              ),
              validator: (value) {
                final parsed = double.tryParse(value?.trim() ?? '');
                if (parsed == null || parsed < 0) {
                  return 'Enter a non-negative number';
                }
                return null;
              },
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
          onPressed: isLoading ? null : () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: isLoading ? null : _submit,
          style: FilledButton.styleFrom(backgroundColor: AppColors.brandPrimary),
          child: isLoading
              ? const SizedBox(
                  height: 18,
                  width: 18,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: Colors.white,
                  ),
                )
              : const Text('Save Count'),
        ),
      ],
    );
  }
}

class _ReceiveStockDialog extends ConsumerStatefulWidget {
  const _ReceiveStockDialog({required this.item});

  final InventoryItem item;

  @override
  ConsumerState<_ReceiveStockDialog> createState() =>
      _ReceiveStockDialogState();
}

class _ReceiveStockDialogState extends ConsumerState<_ReceiveStockDialog> {
  final _formKey = GlobalKey<FormState>();
  final _packagesController = TextEditingController();
  final _supplierReferenceController = TextEditingController();
  Branch? _selectedBranch;

  @override
  void dispose() {
    _packagesController.dispose();
    _supplierReferenceController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false) ||
        _selectedBranch == null) {
      return;
    }

    final succeeded = await ref
        .read(receiveInventoryStockControllerProvider.notifier)
        .submit(
          widget.item.id,
          ReceiveInventoryStockRequest(
            packagesReceived: double.parse(_packagesController.text.trim()),
            branchId: _selectedBranch!.id,
            supplierReference:
                _supplierReferenceController.text.trim().isEmpty
                    ? null
                    : _supplierReferenceController.text.trim(),
          ),
        );

    if (!mounted) return;
    if (succeeded) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final isLoading =
        ref.watch(receiveInventoryStockControllerProvider).isLoading;
    final failure = ref
        .read(receiveInventoryStockControllerProvider.notifier)
        .currentFailure;
    final branchesAsync = ref.watch(selectableBranchesProvider);

    return AlertDialog(
      title: Text('Receive Stock — ${widget.item.name}'),
      content: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            branchesAsync.when(
              loading: () => const LinearProgressIndicator(
                color: AppColors.brandPrimary,
              ),
              error: (error, stackTrace) => Text(
                'Could not load branches: ${describeError(error)}',
                style: const TextStyle(color: AppColors.error),
              ),
              data: (branches) => DropdownButtonFormField<Branch>(
                value: _selectedBranch,
                decoration: const InputDecoration(labelText: 'Branch'),
                items: [
                  for (final branch in branches)
                    DropdownMenuItem(value: branch, child: Text(branch.name)),
                ],
                onChanged: isLoading
                    ? null
                    : (branch) => setState(() => _selectedBranch = branch),
                validator: (value) => value == null ? 'Choose a branch' : null,
              ),
            ),
            const SizedBox(height: AppSpacing.sm),
            TextFormField(
              controller: _packagesController,
              enabled: !isLoading,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: InputDecoration(
                labelText: 'Packages received (${widget.item.packagingUnit})',
              ),
              validator: (value) {
                final parsed = double.tryParse(value?.trim() ?? '');
                if (parsed == null || parsed <= 0) {
                  return 'Enter a positive number';
                }
                return null;
              },
            ),
            const SizedBox(height: AppSpacing.sm),
            TextFormField(
              controller: _supplierReferenceController,
              enabled: !isLoading,
              decoration: const InputDecoration(
                labelText: 'Supplier reference (optional)',
              ),
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
          onPressed: isLoading ? null : () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: isLoading ? null : _submit,
          style: FilledButton.styleFrom(backgroundColor: AppColors.brandPrimary),
          child: isLoading
              ? const SizedBox(
                  height: 18,
                  width: 18,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: Colors.white,
                  ),
                )
              : const Text('Receive'),
        ),
      ],
    );
  }
}
