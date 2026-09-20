import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../onboarding/domain/branch_models.dart';
import '../../../onboarding/presentation/providers/branch_scope_providers.dart';
import '../../domain/inventory_movement_models.dart';
import '../providers/inventory_providers.dart';
import '../../../../core/errors/failure.dart';

/// C3 — records one stock movement, adjusting the item's stock on hand in
/// the same operation. Spoiled requires a reason category, For Return
/// requires a supplier reference — every other field is shared.
class RecordMovementScreen extends ConsumerStatefulWidget {
  const RecordMovementScreen({super.key, this.presetItem});

  /// Preselects the item — e.g. the dashboard's low-stock "reorder"
  /// shortcut jumping straight here with the item already chosen.
  final Item? presetItem;

  @override
  ConsumerState<RecordMovementScreen> createState() =>
      _RecordMovementScreenState();
}

class _RecordMovementScreenState extends ConsumerState<RecordMovementScreen> {
  final _formKey = GlobalKey<FormState>();
  final _quantityController = TextEditingController();
  final _noteController = TextEditingController();
  final _reasonCategoryController = TextEditingController();
  final _supplierReferenceController = TextEditingController();

  Item? _selectedItem;
  Branch? _selectedBranch;
  MovementType _type = MovementType.stockIn;

  @override
  void initState() {
    super.initState();
    _selectedItem = widget.presetItem;
  }

  @override
  void dispose() {
    _quantityController.dispose();
    _noteController.dispose();
    _reasonCategoryController.dispose();
    _supplierReferenceController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false) ||
        _selectedItem == null ||
        _selectedBranch == null) {
      return;
    }

    final controller = ref.read(recordMovementControllerProvider.notifier);
    final succeeded = await controller.record(
      RecordMovementRequest(
        itemId: _selectedItem!.id,
        branchId: _selectedBranch!.id,
        type: _type,
        quantity: double.parse(_quantityController.text.trim()),
        note:
            _noteController.text.trim().isEmpty
                ? null
                : _noteController.text.trim(),
        reasonCategory:
            _reasonCategoryController.text.trim().isEmpty
                ? null
                : _reasonCategoryController.text.trim(),
        supplierReference:
            _supplierReferenceController.text.trim().isEmpty
                ? null
                : _supplierReferenceController.text.trim(),
      ),
    );

    if (!mounted) {
      return;
    }

    if (succeeded) {
      ref.invalidate(movementLogProvider);
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final itemsAsync = ref.watch(itemListProvider);
    final branchesAsync = ref.watch(selectableBranchesProvider);
    final recordState = ref.watch(recordMovementControllerProvider);
    final isLoading = recordState.isLoading;
    final failure =
        ref.read(recordMovementControllerProvider.notifier).currentFailure;

    return Scaffold(
      appBar: AppBar(title: const Text('Record Movement')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 500),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.lg,
                vertical: AppSpacing.md,
              ),
              child: Container(
                padding: const EdgeInsets.all(AppSpacing.lg),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: AppRadius.lgBorder,
                  border: Border.all(color: AppColors.border),
                  boxShadow: AppShadows.subtle,
                ),
                child: Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      itemsAsync.when(
                        loading: () => const LinearProgressIndicator(color: AppColors.brandPrimary),
                        error:
                            (error, stackTrace) =>
                                Text('Could not load items: ${describeError(error)}', style: const TextStyle(color: AppColors.error)),
                        data: (items) {
                          // The dropdown compares values by identity, but a
                          // preset item (from the dashboard's reorder
                          // shortcut) is a different instance than the one
                          // freshly fetched here — resolve by id instead.
                          Item? matchedValue;
                          for (final item in items) {
                            if (item.id == _selectedItem?.id) {
                              matchedValue = item;
                              break;
                            }
                          }
                          return DropdownButtonFormField<Item>(
                            value: matchedValue,
                            decoration: InputDecoration(
                              labelText: 'Item',
                              labelStyle: const TextStyle(color: AppColors.textSecondary),
                              prefixIcon: const Icon(Icons.inventory_2, color: AppColors.brandPrimary),
                              filled: true,
                              fillColor: AppColors.background,
                              border: OutlineInputBorder(
                                borderRadius: AppRadius.mdBorder,
                                borderSide: const BorderSide(color: AppColors.border),
                              ),
                              enabledBorder: OutlineInputBorder(
                                borderRadius: AppRadius.mdBorder,
                                borderSide: const BorderSide(color: AppColors.border),
                              ),
                              focusedBorder: OutlineInputBorder(
                                borderRadius: AppRadius.mdBorder,
                                borderSide: const BorderSide(color: AppColors.brandPrimary, width: 2),
                              ),
                            ),
                            items: [
                              for (final item in items)
                                DropdownMenuItem(
                                  value: item,
                                  child: Text(item.name),
                                ),
                            ],
                            onChanged:
                                isLoading
                                    ? null
                                    : (item) =>
                                        setState(() => _selectedItem = item),
                            validator:
                                (value) =>
                                    value == null ? 'Choose an item' : null,
                          );
                        },
                      ),
                      const SizedBox(height: AppSpacing.md),
                      branchesAsync.when(
                        loading: () => const LinearProgressIndicator(color: AppColors.brandPrimary),
                        error:
                            (error, stackTrace) =>
                                Text('Could not load branches: ${describeError(error)}', style: const TextStyle(color: AppColors.error)),
                        data:
                            (branches) => DropdownButtonFormField<Branch>(
                              value: _selectedBranch,
                              decoration: InputDecoration(
                                labelText: 'Branch',
                                labelStyle: const TextStyle(color: AppColors.textSecondary),
                                prefixIcon: const Icon(Icons.storefront, color: AppColors.brandPrimary),
                                filled: true,
                                fillColor: AppColors.background,
                                border: OutlineInputBorder(
                                  borderRadius: AppRadius.mdBorder,
                                  borderSide: const BorderSide(color: AppColors.border),
                                ),
                                enabledBorder: OutlineInputBorder(
                                  borderRadius: AppRadius.mdBorder,
                                  borderSide: const BorderSide(color: AppColors.border),
                                ),
                                focusedBorder: OutlineInputBorder(
                                  borderRadius: AppRadius.mdBorder,
                                  borderSide: const BorderSide(color: AppColors.brandPrimary, width: 2),
                                ),
                              ),
                              items: [
                                for (final branch in branches)
                                  DropdownMenuItem(
                                    value: branch,
                                    child: Text(branch.name),
                                  ),
                              ],
                              onChanged:
                                  isLoading
                                      ? null
                                      : (branch) => setState(
                                        () => _selectedBranch = branch,
                                      ),
                              validator:
                                  (value) =>
                                      value == null ? 'Choose a branch' : null,
                            ),
                      ),
                      const SizedBox(height: AppSpacing.md),
                      DropdownButtonFormField<MovementType>(
                        value: _type,
                        decoration: InputDecoration(
                          labelText: 'Movement type',
                          labelStyle: const TextStyle(color: AppColors.textSecondary),
                          prefixIcon: const Icon(Icons.swap_horiz, color: AppColors.brandPrimary),
                          filled: true,
                          fillColor: AppColors.background,
                          border: OutlineInputBorder(
                            borderRadius: AppRadius.mdBorder,
                            borderSide: const BorderSide(color: AppColors.border),
                          ),
                          enabledBorder: OutlineInputBorder(
                            borderRadius: AppRadius.mdBorder,
                            borderSide: const BorderSide(color: AppColors.border),
                          ),
                          focusedBorder: OutlineInputBorder(
                            borderRadius: AppRadius.mdBorder,
                            borderSide: const BorderSide(color: AppColors.brandPrimary, width: 2),
                          ),
                        ),
                        items: [
                          // Sale is system-generated only, from a completed Cashier
                          // sale — never a manual entry here.
                          for (final type in MovementType.selectable.where(
                            (type) => type != MovementType.sale,
                          ))
                            DropdownMenuItem(
                              value: type,
                              child: Text(type.label),
                            ),
                        ],
                        onChanged:
                            isLoading
                                ? null
                                : (type) => setState(() => _type = type ?? _type),
                      ),
                      const SizedBox(height: AppSpacing.md),
                      TextFormField(
                        controller: _quantityController,
                        enabled: !isLoading,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                          signed: true,
                        ),
                        decoration: InputDecoration(
                          labelText: 'Quantity',
                          labelStyle: const TextStyle(color: AppColors.textSecondary),
                          prefixIcon: const Icon(Icons.numbers, color: AppColors.brandPrimary),
                          filled: true,
                          fillColor: AppColors.background,
                          border: OutlineInputBorder(
                            borderRadius: AppRadius.mdBorder,
                            borderSide: const BorderSide(color: AppColors.border),
                          ),
                          enabledBorder: OutlineInputBorder(
                            borderRadius: AppRadius.mdBorder,
                            borderSide: const BorderSide(color: AppColors.border),
                          ),
                          focusedBorder: OutlineInputBorder(
                            borderRadius: AppRadius.mdBorder,
                            borderSide: const BorderSide(color: AppColors.brandPrimary, width: 2),
                          ),
                          helperText:
                              _type == MovementType.adjustment
                                  ? 'Negative corrects stock down, positive corrects it up.'
                                  : null,
                        ),
                        validator: (value) {
                          final parsed = double.tryParse(value?.trim() ?? '');
                          if (parsed == null || parsed == 0) {
                            return 'Enter a non-zero quantity';
                          }
                          if (_type != MovementType.adjustment && parsed < 0) {
                            return 'Must be positive for this movement type';
                          }
                          return null;
                        },
                      ),
                      if (_type == MovementType.spoiled) ...[
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _reasonCategoryController,
                          enabled: !isLoading,
                          decoration: InputDecoration(
                            labelText: 'Reason category',
                            labelStyle: const TextStyle(color: AppColors.textSecondary),
                            filled: true,
                            fillColor: AppColors.background,
                            border: OutlineInputBorder(
                              borderRadius: AppRadius.mdBorder,
                              borderSide: const BorderSide(color: AppColors.border),
                            ),
                          ),
                          validator:
                              (value) =>
                                  (value == null || value.trim().isEmpty)
                                      ? 'Required for Spoiled'
                                      : null,
                        ),
                      ],
                      if (_type == MovementType.forReturn) ...[
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _supplierReferenceController,
                          enabled: !isLoading,
                          decoration: InputDecoration(
                            labelText: 'Supplier reference',
                            labelStyle: const TextStyle(color: AppColors.textSecondary),
                            filled: true,
                            fillColor: AppColors.background,
                            border: OutlineInputBorder(
                              borderRadius: AppRadius.mdBorder,
                              borderSide: const BorderSide(color: AppColors.border),
                            ),
                          ),
                          validator:
                              (value) =>
                                  (value == null || value.trim().isEmpty)
                                      ? 'Required for For Return'
                                      : null,
                        ),
                      ],
                      const SizedBox(height: AppSpacing.md),
                      TextFormField(
                        controller: _noteController,
                        enabled: !isLoading,
                        decoration: InputDecoration(
                          labelText: 'Note (optional)',
                          labelStyle: const TextStyle(color: AppColors.textSecondary),
                          filled: true,
                          fillColor: AppColors.background,
                          border: OutlineInputBorder(
                            borderRadius: AppRadius.mdBorder,
                            borderSide: const BorderSide(color: AppColors.border),
                          ),
                        ),
                        maxLines: 2,
                      ),
                      if (failure != null) ...[
                        const SizedBox(height: AppSpacing.md),
                        Container(
                          padding: const EdgeInsets.all(AppSpacing.sm),
                          decoration: BoxDecoration(
                            color: AppColors.cardHover,
                            borderRadius: AppRadius.mdBorder,
                            border: Border.all(color: AppColors.error),
                          ),
                          child: Text(
                            failure.message,
                            style: const TextStyle(
                              color: AppColors.error,
                              fontWeight: FontWeight.w500,
                            ),
                            textAlign: TextAlign.center,
                          ),
                        ),
                      ],
                      const SizedBox(height: AppSpacing.lg),
                      SizedBox(
                        height: 52,
                        child: FilledButton(
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            foregroundColor: AppColors.onBrandPrimary,
                            shape: const RoundedRectangleBorder(
                              borderRadius: AppRadius.mdBorder,
                            ),
                          ),
                          onPressed: isLoading ? null : _submit,
                          child:
                              isLoading
                                  ? const SizedBox(
                                    height: 24,
                                    width: 24,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2.5,
                                      color: AppColors.onBrandPrimary,
                                    ),
                                  )
                                  : const Text(
                                    'Record Movement',
                                    style: TextStyle(
                                      fontSize: 16,
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
