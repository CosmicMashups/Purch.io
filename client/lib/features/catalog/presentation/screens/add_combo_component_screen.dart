import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/category_models.dart';
import '../../domain/item_combo_component_models.dart';
import '../providers/catalog_providers.dart';

class AddComboComponentScreen extends ConsumerStatefulWidget {
  const AddComboComponentScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  ConsumerState<AddComboComponentScreen> createState() =>
      _AddComboComponentScreenState();
}

class _AddComboComponentScreenState
    extends ConsumerState<AddComboComponentScreen> {
  final _formKey = GlobalKey<FormState>();
  final _slotLabelController = TextEditingController();
  final _quantityController = TextEditingController(text: '1');
  final _upchargeController = TextEditingController();
  String? _selectedCategoryId;

  @override
  void dispose() {
    _slotLabelController.dispose();
    _quantityController.dispose();
    _upchargeController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    final selectedCategoryId = _selectedCategoryId;
    if (selectedCategoryId == null) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Choose a category.')));
      return;
    }

    final controller = ref.read(
      createComboComponentControllerProvider(widget.itemId).notifier,
    );
    final succeeded = await controller.create(
      CreateItemComboComponentRequest(
        componentCategoryId: selectedCategoryId,
        slotLabel: _slotLabelController.text.trim(),
        quantity: int.parse(_quantityController.text.trim()),
        substitutionUpchargeAmount:
            _upchargeController.text.trim().isEmpty
                ? null
                : double.parse(_upchargeController.text.trim()),
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
    final categoriesAsync = ref.watch(categoryListProvider);
    final createState = ref.watch(
      createComboComponentControllerProvider(widget.itemId),
    );
    final isLoading = createState.isLoading;
    final failure =
        ref
            .read(
              createComboComponentControllerProvider(widget.itemId).notifier,
            )
            .currentFailure;

    return Scaffold(
      appBar: AppBar(title: Text('Add Combo Slot: ${widget.itemName}')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(32),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextFormField(
                      controller: _slotLabelController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Slot label (e.g. "Choose a Drink")',
                        border: OutlineInputBorder(),
                      ),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
                    ),
                    const SizedBox(height: 16),
                    categoriesAsync.when(
                      loading: () => const CircularProgressIndicator(),
                      error:
                          (error, stackTrace) =>
                              Text('Could not load categories: $error'),
                      data:
                          (categories) => DropdownButtonFormField<String>(
                            value: _selectedCategoryId,
                            decoration: const InputDecoration(
                              labelText: 'Category to choose from',
                              border: OutlineInputBorder(),
                            ),
                            items: [
                              for (final Category category in categories)
                                DropdownMenuItem(
                                  value: category.id,
                                  child: Text(category.name),
                                ),
                            ],
                            onChanged:
                                isLoading
                                    ? null
                                    : (value) => setState(
                                      () => _selectedCategoryId = value,
                                    ),
                          ),
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _quantityController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Quantity',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: TextInputType.number,
                      validator: (value) {
                        final parsed = int.tryParse(value?.trim() ?? '');
                        if (parsed == null || parsed < 1) {
                          return 'Enter a quantity of 1 or more';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _upchargeController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Substitution upcharge (optional)',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      validator: (value) {
                        if (value == null || value.trim().isEmpty) {
                          return null;
                        }
                        final parsed = double.tryParse(value.trim());
                        if (parsed == null || parsed < 0) {
                          return 'Enter a valid amount';
                        }
                        return null;
                      },
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
                                : const Text('Add Combo Slot'),
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
