import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../onboarding/domain/branch_models.dart';
import '../../../onboarding/presentation/providers/onboarding_providers.dart';
import '../../domain/inventory_movement_models.dart';
import '../providers/inventory_providers.dart';

/// C3 — records one stock movement, adjusting the item's stock on hand in
/// the same operation. Spoiled requires a reason category, For Return
/// requires a supplier reference — every other field is shared.
class RecordMovementScreen extends ConsumerStatefulWidget {
  const RecordMovementScreen({super.key});

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
    final branchesAsync = ref.watch(branchListProvider);
    final recordState = ref.watch(recordMovementControllerProvider);
    final isLoading = recordState.isLoading;
    final failure =
        ref.read(recordMovementControllerProvider.notifier).currentFailure;

    return Scaffold(
      appBar: AppBar(title: const Text('Record Movement')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    itemsAsync.when(
                      loading: () => const LinearProgressIndicator(),
                      error:
                          (error, stackTrace) =>
                              Text('Could not load items: $error'),
                      data:
                          (items) => DropdownButtonFormField<Item>(
                            value: _selectedItem,
                            decoration: const InputDecoration(
                              labelText: 'Item',
                              border: OutlineInputBorder(),
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
                          ),
                    ),
                    const SizedBox(height: 16),
                    branchesAsync.when(
                      loading: () => const LinearProgressIndicator(),
                      error:
                          (error, stackTrace) =>
                              Text('Could not load branches: $error'),
                      data:
                          (branches) => DropdownButtonFormField<Branch>(
                            value: _selectedBranch,
                            decoration: const InputDecoration(
                              labelText: 'Branch',
                              border: OutlineInputBorder(),
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
                    const SizedBox(height: 16),
                    DropdownButtonFormField<MovementType>(
                      value: _type,
                      decoration: const InputDecoration(
                        labelText: 'Movement type',
                        border: OutlineInputBorder(),
                      ),
                      items: [
                        for (final type in MovementType.values)
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
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _quantityController,
                      enabled: !isLoading,
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                        signed: true,
                      ),
                      decoration: InputDecoration(
                        labelText: 'Quantity',
                        border: const OutlineInputBorder(),
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
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: _reasonCategoryController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Reason category',
                          border: OutlineInputBorder(),
                        ),
                        validator:
                            (value) =>
                                (value == null || value.trim().isEmpty)
                                    ? 'Required for Spoiled'
                                    : null,
                      ),
                    ],
                    if (_type == MovementType.forReturn) ...[
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: _supplierReferenceController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Supplier reference',
                          border: OutlineInputBorder(),
                        ),
                        validator:
                            (value) =>
                                (value == null || value.trim().isEmpty)
                                    ? 'Required for For Return'
                                    : null,
                      ),
                    ],
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _noteController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Note (optional)',
                        border: OutlineInputBorder(),
                      ),
                      maxLines: 2,
                    ),
                    if (failure != null) ...[
                      const SizedBox(height: 16),
                      Text(
                        failure.message,
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                        textAlign: TextAlign.center,
                      ),
                    ],
                    const SizedBox(height: 24),
                    SizedBox(
                      height: 56,
                      child: FilledButton(
                        onPressed: isLoading ? null : _submit,
                        child:
                            isLoading
                                ? const SizedBox(
                                  height: 24,
                                  width: 24,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2.5,
                                  ),
                                )
                                : const Text('Record Movement'),
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
}
