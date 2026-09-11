import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/hardware/barcode_scanner_screen.dart';
import '../../domain/category_models.dart';
import '../../domain/item_models.dart';
import '../../domain/pricing_type.dart';
import '../providers/catalog_providers.dart';

/// B2's base item form. Pricing-type-specific sub-forms (B2a weight/volume,
/// B2b bundle, B2c service, B3 variant, B4 combo) aren't built yet — picking
/// anything other than Unit here creates an item the POS can't fully use
/// until those land, which is why the dropdown flags that explicitly.
class AddItemScreen extends ConsumerStatefulWidget {
  const AddItemScreen({super.key});

  @override
  ConsumerState<AddItemScreen> createState() => _AddItemScreenState();
}

class _AddItemScreenState extends ConsumerState<AddItemScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _skuController = TextEditingController();
  final _barcodeController = TextEditingController();
  final _priceController = TextEditingController();
  Category? _selectedCategory;
  PricingType _pricingType = PricingType.unit;

  @override
  void dispose() {
    _nameController.dispose();
    _skuController.dispose();
    _barcodeController.dispose();
    _priceController.dispose();
    super.dispose();
  }

  Future<void> _scanBarcode() async {
    final scanned = await Navigator.of(context).push<String>(
      MaterialPageRoute(builder: (_) => const BarcodeScannerScreen()),
    );
    if (scanned != null && mounted) {
      _barcodeController.text = scanned;
    }
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(createItemControllerProvider.notifier);
    final succeeded = await controller.create(
      CreateItemRequest(
        name: _nameController.text.trim(),
        sku:
            _skuController.text.trim().isEmpty
                ? null
                : _skuController.text.trim(),
        barcode:
            _barcodeController.text.trim().isEmpty
                ? null
                : _barcodeController.text.trim(),
        categoryId: _selectedCategory?.id,
        basePrice: double.parse(_priceController.text.trim()),
        pricingType: _pricingType,
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
    final createState = ref.watch(createItemControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createItemControllerProvider.notifier).currentFailure;

    return Scaffold(
      appBar: AppBar(title: const Text('Add Item')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(32),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextFormField(
                      controller: _nameController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Item name',
                        border: OutlineInputBorder(),
                      ),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _priceController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Price',
                        border: OutlineInputBorder(),
                        prefixText: '₱ ',
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      validator: (value) {
                        final parsed = double.tryParse(value?.trim() ?? '');
                        if (parsed == null) {
                          return 'Enter a valid amount';
                        }
                        if (parsed < 0) {
                          return 'Price cannot be negative';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 16),
                    DropdownButtonFormField<PricingType>(
                      value: _pricingType,
                      decoration: const InputDecoration(
                        labelText: 'Pricing type',
                        border: OutlineInputBorder(),
                      ),
                      items:
                          PricingType.values
                              .map(
                                (type) => DropdownMenuItem(
                                  value: type,
                                  child: Text(_pricingLabel(type)),
                                ),
                              )
                              .toList(),
                      onChanged:
                          isLoading
                              ? null
                              : (value) => setState(
                                () => _pricingType = value ?? _pricingType,
                              ),
                    ),
                    if (_pricingType != PricingType.unit) ...[
                      const SizedBox(height: 8),
                      Text(
                        'Only Unit pricing is fully usable in the POS right now — '
                        'the ${_pricingLabel(_pricingType)} setup screens aren\'t built yet.',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                    const SizedBox(height: 16),
                    categoriesAsync.when(
                      loading: () => const LinearProgressIndicator(),
                      error: (error, stackTrace) => const SizedBox.shrink(),
                      data:
                          (categories) => DropdownButtonFormField<Category?>(
                            value: _selectedCategory,
                            decoration: const InputDecoration(
                              labelText: 'Category (optional)',
                              border: OutlineInputBorder(),
                            ),
                            items: [
                              const DropdownMenuItem<Category?>(
                                value: null,
                                child: Text('None'),
                              ),
                              ...categories.map(
                                (category) => DropdownMenuItem(
                                  value: category,
                                  child: Text(category.name),
                                ),
                              ),
                            ],
                            onChanged:
                                isLoading
                                    ? null
                                    : (value) => setState(
                                      () => _selectedCategory = value,
                                    ),
                          ),
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _skuController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'SKU (optional)',
                        border: OutlineInputBorder(),
                      ),
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _barcodeController,
                      enabled: !isLoading,
                      decoration: InputDecoration(
                        labelText: 'Barcode (optional)',
                        border: const OutlineInputBorder(),
                        suffixIcon: IconButton(
                          onPressed: isLoading ? null : _scanBarcode,
                          icon: const Icon(Icons.qr_code_scanner),
                          tooltip: 'Scan barcode',
                        ),
                      ),
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
                                : const Text('Add Item'),
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

  String _pricingLabel(PricingType type) => switch (type) {
    PricingType.unit => 'Unit',
    PricingType.weightVolume => 'Weight/Volume',
    PricingType.bundle => 'Bundle',
    PricingType.service => 'Service',
    PricingType.combo => 'Combo',
    PricingType.variantMatrix => 'Variant Matrix',
  };
}
