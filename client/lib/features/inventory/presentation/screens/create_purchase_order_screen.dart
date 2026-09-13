import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../onboarding/domain/branch_models.dart';
import '../../../onboarding/presentation/providers/onboarding_providers.dart';
import '../../domain/purchase_order_models.dart';
import '../../domain/supplier_models.dart';
import '../providers/purchase_order_providers.dart';
import '../providers/supplier_providers.dart';
import '../../../../core/errors/failure.dart';

class CreatePurchaseOrderScreen extends ConsumerStatefulWidget {
  const CreatePurchaseOrderScreen({super.key});

  @override
  ConsumerState<CreatePurchaseOrderScreen> createState() =>
      _CreatePurchaseOrderScreenState();
}

class _CreatePurchaseOrderScreenState
    extends ConsumerState<CreatePurchaseOrderScreen> {
  Supplier? _supplier;
  Branch? _branch;
  final List<_LineDraft> _lines = [_LineDraft()];

  @override
  void dispose() {
    for (final line in _lines) {
      line.quantityController.dispose();
      line.unitCostController.dispose();
    }
    super.dispose();
  }

  bool get _canSubmit {
    if (_supplier == null || _branch == null) {
      return false;
    }
    for (final line in _lines) {
      final quantity = double.tryParse(line.quantityController.text.trim());
      final unitCost = double.tryParse(line.unitCostController.text.trim());
      if (line.item == null ||
          quantity == null ||
          quantity <= 0 ||
          unitCost == null ||
          unitCost < 0) {
        return false;
      }
    }
    return true;
  }

  Future<void> _submit() async {
    if (!_canSubmit) {
      return;
    }

    final controller = ref.read(createPurchaseOrderControllerProvider.notifier);
    final succeeded = await controller.create(
      CreatePurchaseOrderRequest(
        supplierId: _supplier!.id,
        branchId: _branch!.id,
        lines: [
          for (final line in _lines)
            CreatePurchaseOrderLineRequest(
              itemId: line.item!.id,
              quantityOrdered: double.parse(
                line.quantityController.text.trim(),
              ),
              expectedUnitCost: double.parse(
                line.unitCostController.text.trim(),
              ),
            ),
        ],
      ),
    );

    if (!mounted) {
      return;
    }

    if (succeeded) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final suppliersAsync = ref.watch(supplierListProvider);
    final branchesAsync = ref.watch(branchListProvider);
    final itemsAsync = ref.watch(itemListProvider);
    final createState = ref.watch(createPurchaseOrderControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createPurchaseOrderControllerProvider.notifier).currentFailure;

    return Scaffold(
      appBar: AppBar(title: const Text('New Purchase Order')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 540),
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
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    suppliersAsync.when(
                      loading: () => const LinearProgressIndicator(color: AppColors.brandPrimary),
                      error:
                          (error, stackTrace) =>
                              Text('Could not load suppliers: ${describeError(error)}', style: const TextStyle(color: AppColors.error)),
                      data:
                          (suppliers) => DropdownButtonFormField<Supplier>(
                            value: _matchSupplier(suppliers, _supplier?.id),
                            isExpanded: true,
                            decoration: InputDecoration(
                              labelText: 'Supplier',
                              labelStyle: const TextStyle(color: AppColors.textSecondary),
                              prefixIcon: const Icon(Icons.business, color: AppColors.brandPrimary),
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
                              for (final supplier in suppliers)
                                DropdownMenuItem(
                                  value: supplier,
                                  child: Text(
                                    supplier.name,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                            ],
                            onChanged:
                                isLoading
                                    ? null
                                    : (supplier) =>
                                        setState(() => _supplier = supplier),
                          ),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    branchesAsync.when(
                      loading: () => const LinearProgressIndicator(color: AppColors.brandPrimary),
                      error:
                          (error, stackTrace) =>
                              Text('Could not load branches: ${describeError(error)}', style: const TextStyle(color: AppColors.error)),
                      data:
                          (branches) => DropdownButtonFormField<Branch>(
                            value: _matchBranch(branches, _branch?.id),
                            isExpanded: true,
                            decoration: InputDecoration(
                              labelText: 'Deliver to branch',
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
                                  child: Text(
                                    branch.name,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                            ],
                            onChanged:
                                isLoading
                                    ? null
                                    : (branch) =>
                                        setState(() => _branch = branch),
                          ),
                    ),
                    const SizedBox(height: AppSpacing.lg),
                    Text(
                      'Items',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    itemsAsync.when(
                      loading: () => const LinearProgressIndicator(color: AppColors.brandPrimary),
                      error:
                          (error, stackTrace) =>
                              Text('Could not load items: ${describeError(error)}', style: const TextStyle(color: AppColors.error)),
                      data:
                          (items) => Column(
                            children: [
                              for (var i = 0; i < _lines.length; i++)
                                _LineRow(
                                  key: ValueKey(_lines[i]),
                                  items: items,
                                  draft: _lines[i],
                                  isLoading: isLoading,
                                  onChanged: () => setState(() {}),
                                  onRemove:
                                      _lines.length > 1
                                          ? () =>
                                              setState(() => _lines.removeAt(i))
                                          : null,
                                ),
                              Align(
                                alignment: Alignment.centerLeft,
                                child: TextButton.icon(
                                  onPressed:
                                      isLoading
                                          ? null
                                          : () => setState(
                                            () => _lines.add(_LineDraft()),
                                          ),
                                  icon: const Icon(Icons.add, size: 18),
                                  label: const Text('Add Item'),
                                ),
                              ),
                            ],
                          ),
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
                        onPressed: (!isLoading && _canSubmit) ? _submit : null,
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
                                  'Create Purchase Order',
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
    );
  }

  Supplier? _matchSupplier(List<Supplier> suppliers, String? id) {
    if (id == null) {
      return null;
    }
    for (final supplier in suppliers) {
      if (supplier.id == id) {
        return supplier;
      }
    }
    return null;
  }

  Branch? _matchBranch(List<Branch> branches, String? id) {
    if (id == null) {
      return null;
    }
    for (final branch in branches) {
      if (branch.id == id) {
        return branch;
      }
    }
    return null;
  }
}

class _LineDraft {
  Item? item;
  final quantityController = TextEditingController();
  final unitCostController = TextEditingController();
}

class _LineRow extends StatelessWidget {
  const _LineRow({
    super.key,
    required this.items,
    required this.draft,
    required this.isLoading,
    required this.onChanged,
    required this.onRemove,
  });

  final List<Item> items;
  final _LineDraft draft;
  final bool isLoading;
  final VoidCallback onChanged;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    Item? matchedItem;
    for (final item in items) {
      if (item.id == draft.item?.id) {
        matchedItem = item;
        break;
      }
    }

    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: DropdownButtonFormField<Item>(
                  value: matchedItem,
                  isExpanded: true,
                  decoration: const InputDecoration(
                    labelText: 'Item',
                    border: OutlineInputBorder(),
                    isDense: true,
                  ),
                  items: [
                    for (final item in items)
                      DropdownMenuItem(
                        value: item,
                        child: Text(item.name, overflow: TextOverflow.ellipsis),
                      ),
                  ],
                  onChanged:
                      isLoading
                          ? null
                          : (item) {
                            draft.item = item;
                            onChanged();
                          },
                ),
              ),
              if (onRemove != null)
                IconButton(
                  onPressed: isLoading ? null : onRemove,
                  icon: const Icon(Icons.remove_circle_outline),
                  tooltip: 'Remove item',
                ),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: draft.quantityController,
                  enabled: !isLoading,
                  decoration: const InputDecoration(
                    labelText: 'Qty',
                    border: OutlineInputBorder(),
                    isDense: true,
                  ),
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  onChanged: (_) => onChanged(),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: TextField(
                  controller: draft.unitCostController,
                  enabled: !isLoading,
                  decoration: const InputDecoration(
                    labelText: 'Expected unit cost',
                    border: OutlineInputBorder(),
                    isDense: true,
                    prefixText: '₱ ',
                  ),
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  onChanged: (_) => onChanged(),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
