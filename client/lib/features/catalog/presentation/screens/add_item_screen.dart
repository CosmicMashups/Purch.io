import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/hardware/barcode_scanner_screen.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/image_upload_field.dart';
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
  final _imageUrlController = TextEditingController();
  Category? _selectedCategory;
  PricingType _pricingType = PricingType.unit;

  @override
  void dispose() {
    _nameController.dispose();
    _skuController.dispose();
    _barcodeController.dispose();
    _priceController.dispose();
    _imageUrlController.dispose();
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
        imageUrl:
            _imageUrlController.text.trim().isEmpty
                ? null
                : _imageUrlController.text.trim(),
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
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Add Item'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 540),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.md,
                vertical: AppSpacing.sm,
              ),
              child: Card(
                elevation: 0,
                color: AppColors.surface,
                shape: RoundedRectangleBorder(
                  borderRadius: AppRadius.lgBorder,
                  side: const BorderSide(color: AppColors.border),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.md),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        TextFormField(
                          controller: _nameController,
                          enabled: !isLoading,
                          decoration: InputDecoration(
                            labelText: 'Item name',
                            isDense: true,
                            hintText: 'e.g. Bottled Water 500ml',
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
                              borderSide: const BorderSide(
                                color: AppColors.brandPrimary,
                                width: 2,
                              ),
                            ),
                            filled: true,
                            fillColor: AppColors.cardHover,
                          ),
                          validator:
                              (value) =>
                                  (value == null || value.trim().isEmpty)
                                      ? 'Required'
                                      : null,
                        ),
                        const SizedBox(height: AppSpacing.sm),
                        TextFormField(
                          controller: _priceController,
                          enabled: !isLoading,
                          style: const TextStyle(
                            fontFeatures: [FontFeature.tabularFigures()],
                            fontWeight: FontWeight.w600,
                          ),
                          decoration: InputDecoration(
                            labelText: 'Price',
                            isDense: true,
                            hintText: '0.00',
                            prefixText: '₱ ',
                            prefixStyle: const TextStyle(
                              color: AppColors.brandPrimary,
                              fontWeight: FontWeight.w700,
                            ),
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
                              borderSide: const BorderSide(
                                color: AppColors.brandPrimary,
                                width: 2,
                              ),
                            ),
                            filled: true,
                            fillColor: AppColors.cardHover,
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
                        const SizedBox(height: AppSpacing.sm),
                        DropdownButtonFormField<PricingType>(
                          value: _pricingType,
                          decoration: InputDecoration(
                            labelText: 'Pricing type',
                            isDense: true,
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
                              borderSide: const BorderSide(
                                color: AppColors.brandPrimary,
                                width: 2,
                              ),
                            ),
                            filled: true,
                            fillColor: AppColors.cardHover,
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
                          const SizedBox(height: AppSpacing.xs),
                          Padding(
                            padding: const EdgeInsets.only(left: 4),
                            child: Text(
                              'Only Unit pricing is fully usable in the POS right now — '
                              'the ${_pricingLabel(_pricingType)} setup screens aren\'t built yet.',
                              style: Theme.of(context).textTheme.bodySmall
                                  ?.copyWith(color: AppColors.accentWarm),
                            ),
                          ),
                        ],
                        const SizedBox(height: AppSpacing.sm),
                        categoriesAsync.when(
                          loading: () => const LinearProgressIndicator(),
                          error: (error, stackTrace) => const SizedBox.shrink(),
                          data:
                              (categories) => DropdownButtonFormField<Category?>(
                                value: _selectedCategory,
                                decoration: InputDecoration(
                                  labelText: 'Category (optional)',
                                  isDense: true,
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
                                    borderSide: const BorderSide(
                                      color: AppColors.brandPrimary,
                                      width: 2,
                                    ),
                                  ),
                                  filled: true,
                                  fillColor: AppColors.cardHover,
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
                        const SizedBox(height: AppSpacing.sm),
                        TextFormField(
                          controller: _skuController,
                          enabled: !isLoading,
                          decoration: InputDecoration(
                            labelText: 'SKU (optional)',
                            isDense: true,
                            hintText: 'e.g. SKU-BEV-001',
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
                              borderSide: const BorderSide(
                                color: AppColors.brandPrimary,
                                width: 2,
                              ),
                            ),
                            filled: true,
                            fillColor: AppColors.cardHover,
                          ),
                        ),
                        const SizedBox(height: AppSpacing.sm),
                        TextFormField(
                          controller: _barcodeController,
                          enabled: !isLoading,
                          decoration: InputDecoration(
                            labelText: 'Barcode (optional)',
                            isDense: true,
                            hintText: 'Scan or type barcode',
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
                              borderSide: const BorderSide(
                                color: AppColors.brandPrimary,
                                width: 2,
                              ),
                            ),
                            filled: true,
                            fillColor: AppColors.cardHover,
                            suffixIcon: IconButton(
                              onPressed: isLoading ? null : _scanBarcode,
                              icon: const Icon(
                                Icons.qr_code_scanner,
                                color: AppColors.brandPrimary,
                              ),
                              tooltip: 'Scan barcode',
                            ),
                          ),
                        ),
                        const SizedBox(height: AppSpacing.md),
                        ImageUploadField(
                          controller: _imageUrlController,
                          label: 'Item image (optional)',
                          enabled: !isLoading,
                          hintText: 'e.g. /uploads/... or https://... or assets/images/...',
                          onChanged: () => setState(() {}),
                        ),
                        const SizedBox(height: AppSpacing.xs),
                        Wrap(
                          spacing: 6,
                          runSpacing: 4,
                          children: [
                            ActionChip(
                              label: const Text('Rice Bowl', style: TextStyle(fontSize: 11)),
                              avatar: const Icon(Icons.lunch_dining_rounded, size: 14),
                              visualDensity: VisualDensity.compact,
                              padding: EdgeInsets.zero,
                              onPressed: () {
                                _imageUrlController.text = 'assets/images/combo_rice_bowl.jpg';
                                setState(() {});
                              },
                            ),
                            ActionChip(
                              label: const Text('Iced Latte', style: TextStyle(fontSize: 11)),
                              avatar: const Icon(Icons.local_cafe_rounded, size: 14),
                              visualDensity: VisualDensity.compact,
                              padding: EdgeInsets.zero,
                              onPressed: () {
                                _imageUrlController.text = 'assets/images/beverage_iced_latte.jpg';
                                setState(() {});
                              },
                            ),
                          ],
                        ),
                        if (failure != null) ...[
                          const SizedBox(height: AppSpacing.lg),
                          Container(
                            padding: const EdgeInsets.all(AppSpacing.md),
                            decoration: BoxDecoration(
                              color: AppColors.accentWarm.withValues(alpha: 0.1),
                              borderRadius: AppRadius.mdBorder,
                              border: Border.all(
                                color: AppColors.accentWarm.withValues(alpha: 0.3),
                              ),
                            ),
                            child: Text(
                              failure.message,
                              style: const TextStyle(
                                color: AppColors.accentWarm,
                                fontWeight: FontWeight.w600,
                              ),
                              textAlign: TextAlign.center,
                            ),
                          ),
                        ],
                        const SizedBox(height: AppSpacing.xl),
                        SizedBox(
                          height: 52,
                          child: FilledButton(
                            style: FilledButton.styleFrom(
                              backgroundColor: AppColors.brandPrimary,
                              shape: RoundedRectangleBorder(
                                borderRadius: AppRadius.mdBorder,
                              ),
                            ),
                            onPressed: isLoading ? null : _submit,
                            child:
                                isLoading
                                    ? const SizedBox(
                                      height: 22,
                                      width: 22,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2.5,
                                        color: Colors.white,
                                      ),
                                    )
                                    : const Text(
                                      'Add Item',
                                      style: TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w600,
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
