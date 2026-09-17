import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/hardware/barcode_scanner_screen.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/image_upload_field.dart';
import '../../domain/bundle_promo_rule_models.dart';
import '../../domain/category_models.dart';
import '../../domain/item_combo_component_models.dart';
import '../../domain/item_models.dart';
import '../../domain/item_variant_models.dart';
import '../../domain/pricing_type.dart';
import '../../domain/tingi_mode.dart';
import '../providers/catalog_providers.dart';

class _AttributePair {
  _AttributePair({String key = '', String value = ''})
      : keyController = TextEditingController(text: key),
        valueController = TextEditingController(text: value);

  final TextEditingController keyController;
  final TextEditingController valueController;

  void dispose() {
    keyController.dispose();
    valueController.dispose();
  }
}

class _SizeRow {
  _SizeRow([String initialValue = ''])
      : controller = TextEditingController(text: initialValue);

  final TextEditingController controller;

  void dispose() => controller.dispose();
}

/// Catalog item creation screen with full contextual configuration support
/// across all pricing models: Unit, Weight/Volume (Tingi), Bundle, Service,
/// Combo, and Variant Matrix.
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

  // --- Weight / Volume Sub-Form ---
  TingiMode _tingiMode = TingiMode.none;
  final _packagedSizeController = TextEditingController();
  final _incrementStepController = TextEditingController();
  final List<_SizeRow> _sizeRows = [_SizeRow()];

  // --- Bundle Sub-Form ---
  final _bundleTriggerQtyController = TextEditingController();
  final _bundlePriceController = TextEditingController();
  final _bundleDescController = TextEditingController();

  // --- Service Sub-Form ---
  final _serviceDurationController = TextEditingController();

  // --- Variant Matrix Sub-Form ---
  final _variantSkuController = TextEditingController();
  final _variantPriceOverrideController = TextEditingController();
  final List<_AttributePair> _variantAttributes = [
    _AttributePair(key: 'Size', value: 'Regular'),
  ];

  // --- Combo Sub-Form ---
  final _comboSlotLabelController = TextEditingController();
  final _comboQtyController = TextEditingController(text: '1');
  final _comboUpchargeController = TextEditingController();
  Category? _comboCategory;

  bool _isSaving = false;
  String? _customError;

  @override
  void dispose() {
    _nameController.dispose();
    _skuController.dispose();
    _barcodeController.dispose();
    _priceController.dispose();
    _imageUrlController.dispose();

    _packagedSizeController.dispose();
    _incrementStepController.dispose();
    for (final row in _sizeRows) {
      row.dispose();
    }

    _bundleTriggerQtyController.dispose();
    _bundlePriceController.dispose();
    _bundleDescController.dispose();

    _serviceDurationController.dispose();

    _variantSkuController.dispose();
    _variantPriceOverrideController.dispose();
    for (final pair in _variantAttributes) {
      pair.dispose();
    }

    _comboSlotLabelController.dispose();
    _comboQtyController.dispose();
    _comboUpchargeController.dispose();

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

    setState(() {
      _isSaving = true;
      _customError = null;
    });

    final repository = ref.read(catalogRepositoryProvider);

    try {
      // 1. Create the base item
      final createdItem = await repository.createItem(
        CreateItemRequest(
          name: _nameController.text.trim(),
          sku: _skuController.text.trim().isEmpty
              ? null
              : _skuController.text.trim(),
          barcode: _barcodeController.text.trim().isEmpty
              ? null
              : _barcodeController.text.trim(),
          categoryId: _selectedCategory?.id,
          basePrice: double.parse(_priceController.text.trim()),
          imageUrl: _imageUrlController.text.trim().isEmpty
              ? null
              : _imageUrlController.text.trim(),
          pricingType: _pricingType,
        ),
      );

      // 2. Perform sequential sub-resource creation if configured
      if (_pricingType == PricingType.weightVolume) {
        if (_tingiMode != TingiMode.none) {
          final packSize = double.parse(_packagedSizeController.text.trim());
          if (_tingiMode == TingiMode.increment) {
            final step = double.parse(_incrementStepController.text.trim());
            await repository.updateTingiConfig(
              createdItem.id,
              UpdateTingiConfigRequest(
                tingiMode: TingiMode.increment,
                packagedSize: packSize,
                tingiIncrementStep: step,
              ),
            );
          } else if (_tingiMode == TingiMode.fixedSizes) {
            final sizes = _sizeRows
                .map((r) => double.tryParse(r.controller.text.trim()))
                .whereType<double>()
                .where((s) => s > 0 && s <= packSize)
                .toList();
            if (sizes.isNotEmpty) {
              await repository.updateTingiConfig(
                createdItem.id,
                UpdateTingiConfigRequest(
                  tingiMode: TingiMode.fixedSizes,
                  packagedSize: packSize,
                  allowedSizes: sizes,
                ),
              );
            }
          }
        }
      } else if (_pricingType == PricingType.service) {
        final duration =
            int.tryParse(_serviceDurationController.text.trim()) ?? 30;
        await repository.updateServiceDuration(
          createdItem.id,
          UpdateServiceDurationRequest(durationMinutes: duration),
        );
      } else if (_pricingType == PricingType.bundle) {
        final triggerQty =
            int.tryParse(_bundleTriggerQtyController.text.trim());
        final bundlePrice =
            double.tryParse(_bundlePriceController.text.trim());
        if (triggerQty != null && triggerQty > 0 && bundlePrice != null) {
          final desc = _bundleDescController.text.trim().isEmpty
              ? 'Buy $triggerQty for ₱${bundlePrice.toStringAsFixed(2)}'
              : _bundleDescController.text.trim();
          await repository.createBundleRule(
            createdItem.id,
            CreateBundlePromoRuleRequest(
              triggerQuantity: triggerQty,
              bundlePrice: bundlePrice,
              description: desc,
            ),
          );
        }
      } else if (_pricingType == PricingType.variantMatrix) {
        final attrs = <String, String>{};
        for (final pair in _variantAttributes) {
          final k = pair.keyController.text.trim();
          final v = pair.valueController.text.trim();
          if (k.isNotEmpty && v.isNotEmpty) {
            attrs[k] = v;
          }
        }
        if (attrs.isNotEmpty) {
          final priceOverride = double.tryParse(
            _variantPriceOverrideController.text.trim(),
          );
          await repository.createVariant(
            createdItem.id,
            CreateItemVariantRequest(
              attributes: attrs,
              sku: _variantSkuController.text.trim().isEmpty
                  ? null
                  : _variantSkuController.text.trim(),
              priceOverride: priceOverride,
              imageUrl: createdItem.imageUrl,
            ),
          );
        }
      } else if (_pricingType == PricingType.combo) {
        final slotLabel = _comboSlotLabelController.text.trim();
        final slotCat = _comboCategory;
        final qty = int.tryParse(_comboQtyController.text.trim()) ?? 1;
        final upcharge =
            double.tryParse(_comboUpchargeController.text.trim());
        if (slotLabel.isNotEmpty && slotCat != null) {
          await repository.createComboComponent(
            createdItem.id,
            CreateItemComboComponentRequest(
              componentCategoryId: slotCat.id,
              slotLabel: slotLabel,
              quantity: qty,
              substitutionUpchargeAmount: upcharge,
            ),
          );
        }
      }

      await ref.read(itemListProvider.notifier).refresh();

      if (!mounted) {
        return;
      }
      Navigator.of(context).pop();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isSaving = false;
        _customError = e is Failure ? e.message : e.toString();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final categoriesAsync = ref.watch(categoryListProvider);
    final isLoading = _isSaving;
    final failure = _customError;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text(
          'Add Item',
          style: TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w700,
            letterSpacing: -0.3,
            color: AppColors.textPrimary,
          ),
        ),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
        shape: const Border(
          bottom: BorderSide(color: AppColors.border, width: 1),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 580),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.md,
                vertical: AppSpacing.lg,
              ),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                  // SECTION 1: Item Core Identity & Base Pricing
                  _buildFormSection(
                    title: 'Core Details',
                    subtitle: 'Item naming, baseline price, and department classification',
                    icon: Icons.storefront_rounded,
                    children: [
                      TextFormField(
                        controller: _nameController,
                        enabled: !isLoading,
                        style: const TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                        decoration: _buildInputDecoration(
                          label: 'Item name',
                          hintText: 'e.g. Bottled Water 500ml',
                          prefixIcon: Icons.shopping_bag_outlined,
                        ),
                        validator: (value) =>
                            (value == null || value.trim().isEmpty)
                                ? 'Required'
                                : null,
                      ),
                      const SizedBox(height: AppSpacing.md),
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Expanded(
                            flex: 6,
                            child: TextFormField(
                              controller: _priceController,
                              enabled: !isLoading,
                              style: const TextStyle(
                                fontSize: 16,
                                fontFeatures: [FontFeature.tabularFigures()],
                                fontWeight: FontWeight.w700,
                                color: AppColors.brandPrimary,
                              ),
                              decoration: _buildInputDecoration(
                                label: 'Price',
                                hintText: '0.00',
                                prefixText: '₱ ',
                                prefixStyle: const TextStyle(
                                  color: AppColors.brandPrimary,
                                  fontWeight: FontWeight.w800,
                                  fontSize: 16,
                                ),
                              ),
                              keyboardType:
                                  const TextInputType.numberWithOptions(decimal: true),
                              validator: (value) {
                                final parsed =
                                    double.tryParse(value?.trim() ?? '');
                                if (parsed == null) {
                                  return 'Enter a valid amount';
                                }
                                if (parsed < 0) {
                                  return 'Price cannot be negative';
                                }
                                return null;
                              },
                            ),
                          ),
                          const SizedBox(width: AppSpacing.sm),
                          Expanded(
                            flex: 6,
                            child: categoriesAsync.when(
                              loading: () => const SizedBox(
                                height: 48,
                                child: Center(
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                ),
                              ),
                              error: (error, stackTrace) => const SizedBox.shrink(),
                              data: (categories) =>
                                  DropdownButtonFormField<Category?>(
                                value: _selectedCategory,
                                isExpanded: true,
                                decoration: _buildInputDecoration(
                                  label: 'Category (optional)',
                                  prefixIcon: Icons.folder_open_rounded,
                                ),
                                items: [
                                  const DropdownMenuItem<Category?>(
                                    value: null,
                                    child: Text(
                                      'None (Uncategorized)',
                                      style: TextStyle(
                                        color: AppColors.textSecondary,
                                        fontSize: 14,
                                      ),
                                    ),
                                  ),
                                  ...categories.map(
                                    (category) => DropdownMenuItem(
                                      value: category,
                                      child: Text(
                                        category.name,
                                        style: const TextStyle(
                                          fontWeight: FontWeight.w600,
                                          fontSize: 14,
                                        ),
                                        overflow: TextOverflow.ellipsis,
                                      ),
                                    ),
                                  ),
                                ],
                                onChanged: isLoading
                                    ? null
                                    : (value) => setState(
                                          () => _selectedCategory = value,
                                        ),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                  const SizedBox(height: AppSpacing.md),

                  // SECTION 2: Pricing & Operational Model
                  _buildFormSection(
                    title: 'Pricing Model',
                    subtitle: 'Determines how cashier & POS calculate portions, promos, or durations',
                    icon: Icons.sell_rounded,
                    children: [
                      DropdownButtonFormField<PricingType>(
                        value: _pricingType,
                        isExpanded: true,
                        decoration: _buildInputDecoration(
                          label: 'Pricing type',
                          prefixIcon: _pricingIcon(_pricingType),
                        ),
                        items: PricingType.values
                            .map(
                              (type) => DropdownMenuItem(
                                value: type,
                                child: Row(
                                  children: [
                                    Icon(
                                      _pricingIcon(type),
                                      size: 16,
                                      color: AppColors.brandPrimary,
                                    ),
                                    const SizedBox(width: AppSpacing.sm),
                                    Text(
                                      _pricingLabel(type),
                                      style: const TextStyle(
                                        fontWeight: FontWeight.w600,
                                        fontSize: 14,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            )
                            .toList(),
                        onChanged: isLoading
                            ? null
                            : (value) => setState(
                                  () => _pricingType = value ?? _pricingType,
                                ),
                      ),
                      if (_pricingType != PricingType.unit) ...[
                        const SizedBox(height: AppSpacing.md),
                        _buildPricingSubForm(categoriesAsync),
                      ],
                    ],
                  ),
                  const SizedBox(height: AppSpacing.md),

                  // SECTION 3: Inventory Identifiers & Media
                  _buildFormSection(
                    title: 'SKU & Media',
                    subtitle: 'Barcode scanning, stock codes, and product photography',
                    icon: Icons.qr_code_2_rounded,
                    children: [
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Expanded(
                            child: TextFormField(
                              controller: _skuController,
                              enabled: !isLoading,
                              decoration: _buildInputDecoration(
                                label: 'SKU (optional)',
                                hintText: 'e.g. SKU-BEV-001',
                                prefixIcon: Icons.tag_rounded,
                              ),
                            ),
                          ),
                          const SizedBox(width: AppSpacing.sm),
                          Expanded(
                            child: TextFormField(
                              controller: _barcodeController,
                              enabled: !isLoading,
                              decoration: _buildInputDecoration(
                                label: 'Barcode (optional)',
                                hintText: 'Scan or type',
                                prefixIcon: Icons.qr_code_rounded,
                                suffixIcon: IconButton(
                                  onPressed: isLoading ? null : _scanBarcode,
                                  icon: const Icon(
                                    Icons.qr_code_scanner,
                                    color: AppColors.brandPrimary,
                                  ),
                                  tooltip: 'Scan barcode with camera',
                                ),
                              ),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: AppSpacing.md),
                      ImageUploadField(
                        controller: _imageUrlController,
                        label: 'Item image (optional)',
                        enabled: !isLoading,
                        hintText:
                            'e.g. /uploads/... or https://... or assets/images/...',
                        onChanged: () => setState(() {}),
                      ),
                      const SizedBox(height: AppSpacing.xs),
                      Row(
                        children: [
                          Text(
                            'Quick presets: ',
                            style: TextStyle(
                              fontSize: 11,
                              color: AppColors.textSecondary.withValues(alpha: 0.8),
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                          const SizedBox(width: AppSpacing.xs),
                          Wrap(
                            spacing: 6,
                            runSpacing: 4,
                            children: [
                              ActionChip(
                                label: const Text('Rice Bowl',
                                    style: TextStyle(
                                        fontSize: 11,
                                        fontWeight: FontWeight.w600)),
                                avatar: const Icon(Icons.lunch_dining_rounded,
                                    size: 14, color: AppColors.brandPrimary),
                                visualDensity: VisualDensity.compact,
                                backgroundColor: AppColors.brandPrimaryContainer,
                                side: BorderSide(
                                  color: AppColors.brandPrimary
                                      .withValues(alpha: 0.2),
                                ),
                                padding: EdgeInsets.zero,
                                onPressed: () {
                                  _imageUrlController.text =
                                      'assets/images/combo_rice_bowl.jpg';
                                  setState(() {});
                                },
                              ),
                              ActionChip(
                                label: const Text('Iced Latte',
                                    style: TextStyle(
                                        fontSize: 11,
                                        fontWeight: FontWeight.w600)),
                                avatar: const Icon(Icons.local_cafe_rounded,
                                    size: 14, color: AppColors.brandPrimary),
                                visualDensity: VisualDensity.compact,
                                backgroundColor: AppColors.brandPrimaryContainer,
                                side: BorderSide(
                                  color: AppColors.brandPrimary
                                      .withValues(alpha: 0.2),
                                ),
                                padding: EdgeInsets.zero,
                                onPressed: () {
                                  _imageUrlController.text =
                                      'assets/images/beverage_iced_latte.jpg';
                                  setState(() {});
                                },
                              ),
                            ],
                          ),
                        ],
                      ),
                    ],
                  ),

                  if (failure != null) ...[
                    const SizedBox(height: AppSpacing.md),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: AppSpacing.md,
                        vertical: AppSpacing.sm + 2,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.errorContainer.withValues(alpha: 0.6),
                        borderRadius: AppRadius.mdBorder,
                        border: Border.all(
                          color: AppColors.errorBorder,
                        ),
                      ),
                      child: Row(
                        children: [
                          const Icon(
                            Icons.error_outline_rounded,
                            size: 18,
                            color: AppColors.error,
                          ),
                          const SizedBox(width: AppSpacing.sm),
                          Expanded(
                            child: Text(
                              failure,
                              style: const TextStyle(
                                color: AppColors.onErrorContainer,
                                fontWeight: FontWeight.w600,
                                fontSize: 13,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],

                  const SizedBox(height: AppSpacing.xl),

                  // Tactile Submit Action Button
                  Container(
                    height: 52,
                    decoration: BoxDecoration(
                      borderRadius: AppRadius.mdBorder,
                      boxShadow: isLoading ? null : AppShadows.tactileButton,
                    ),
                    child: FilledButton(
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColors.brandPrimary,
                        foregroundColor: Colors.white,
                        elevation: 0,
                        shape: RoundedRectangleBorder(
                          borderRadius: AppRadius.mdBorder,
                        ),
                      ),
                      onPressed: isLoading ? null : _submit,
                      child: isLoading
                          ? const SizedBox(
                              height: 22,
                              width: 22,
                              child: CircularProgressIndicator(
                                strokeWidth: 2.5,
                                color: Colors.white,
                              ),
                            )
                          : const Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Icon(Icons.check_circle_outline_rounded,
                                    size: 19),
                                SizedBox(width: AppSpacing.sm),
                                Text(
                                  'Add Item',
                                  style: TextStyle(
                                    fontSize: 16,
                                    fontWeight: FontWeight.w700,
                                    letterSpacing: 0.2,
                                  ),
                                ),
                              ],
                            ),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.xxl),
                ],
              ),
            ),
          ),
        ),
      ),
    ),
  );
}

  Widget _buildFormSection({
    required String title,
    required String subtitle,
    required IconData icon,
    required List<Widget> children,
  }) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.lgBorder,
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.subtle,
      ),
      padding: const EdgeInsets.all(AppSpacing.md + 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Container(
                width: 32,
                height: 32,
                decoration: BoxDecoration(
                  color: AppColors.brandPrimaryContainer,
                  borderRadius: AppRadius.smBorder,
                ),
                child: Icon(icon, size: 18, color: AppColors.brandPrimary),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                        letterSpacing: -0.2,
                      ),
                    ),
                    Text(
                      subtitle,
                      style: const TextStyle(
                        fontSize: 11,
                        color: AppColors.textSecondary,
                        fontWeight: FontWeight.w400,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.md),
          ...children,
        ],
      ),
    );
  }

  InputDecoration _buildInputDecoration({
    required String label,
    String? hintText,
    IconData? prefixIcon,
    String? prefixText,
    TextStyle? prefixStyle,
    Widget? suffixIcon,
  }) {
    return InputDecoration(
      labelText: label,
      hintText: hintText,
      prefixIcon: prefixIcon != null ? Icon(prefixIcon, size: 18) : null,
      prefixText: prefixText,
      prefixStyle: prefixStyle,
      suffixIcon: suffixIcon,
      isDense: true,
      contentPadding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.md,
        vertical: 13,
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
      fillColor: AppColors.cardHover.withValues(alpha: 0.4),
    );
  }

  IconData _pricingIcon(PricingType type) => switch (type) {
        PricingType.unit => Icons.check_box_outline_blank_rounded,
        PricingType.weightVolume => Icons.scale_rounded,
        PricingType.bundle => Icons.inventory_rounded,
        PricingType.service => Icons.schedule_rounded,
        PricingType.combo => Icons.fastfood_rounded,
        PricingType.variantMatrix => Icons.tune_rounded,
      };

  Widget _buildPricingSubForm(AsyncValue<List<Category>> categoriesAsync) {
    return switch (_pricingType) {
      PricingType.weightVolume => _buildWeightVolumeCard(),
      PricingType.bundle => _buildBundleCard(),
      PricingType.service => _buildServiceCard(),
      PricingType.variantMatrix => _buildVariantMatrixCard(),
      PricingType.combo => _buildComboCard(categoriesAsync),
      PricingType.unit => const SizedBox.shrink(),
    };
  }

  Widget _buildSubFormContainer({
    required String title,
    required String badgeTag,
    required IconData icon,
    required Widget child,
  }) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        border: Border.all(
          color: AppColors.brandPrimary.withValues(alpha: 0.25),
        ),
        boxShadow: const [
          BoxShadow(
            color: Color(0x0A0F766E),
            blurRadius: 6,
            offset: Offset(0, 2),
          ),
        ],
      ),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Container(
                width: 28,
                height: 28,
                decoration: BoxDecoration(
                  color: AppColors.brandPrimaryContainer,
                  borderRadius: AppRadius.smBorder,
                ),
                child: Icon(icon, size: 16, color: AppColors.brandPrimary),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  title,
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                    letterSpacing: -0.2,
                  ),
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 7,
                  vertical: 2.5,
                ),
                decoration: BoxDecoration(
                  color: AppColors.brandPrimaryContainer,
                  borderRadius: BorderRadius.circular(AppRadius.full),
                  border: Border.all(
                    color: AppColors.brandPrimary.withValues(alpha: 0.25),
                  ),
                ),
                child: Text(
                  badgeTag,
                  style: const TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w700,
                    color: AppColors.brandPrimary,
                    letterSpacing: 0.5,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.md),
          child,
        ],
      ),
    );
  }

  Widget _buildWeightVolumeCard() {
    return _buildSubFormContainer(
      title: 'Weight / Volume Configuration (Tingi)',
      badgeTag: 'SCALE READY',
      icon: Icons.scale_rounded,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SegmentedButton<TingiMode>(
            style: ButtonStyle(
              visualDensity: VisualDensity.compact,
              shape: const WidgetStatePropertyAll(
                RoundedRectangleBorder(borderRadius: AppRadius.smBorder),
              ),
              backgroundColor: WidgetStateProperty.resolveWith((states) {
                if (states.contains(WidgetState.selected)) {
                  return AppColors.brandPrimaryContainer;
                }
                return AppColors.surface;
              }),
              foregroundColor: WidgetStateProperty.resolveWith((states) {
                if (states.contains(WidgetState.selected)) {
                  return AppColors.brandPrimary;
                }
                return AppColors.textSecondary;
              }),
            ),
            segments: const [
              ButtonSegment(
                value: TingiMode.none,
                label: Text('Whole pack', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 12)),
              ),
              ButtonSegment(
                value: TingiMode.increment,
                label: Text('Step increment', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 12)),
              ),
              ButtonSegment(
                value: TingiMode.fixedSizes,
                label: Text('Fixed portions', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 12)),
              ),
            ],
            selected: {_tingiMode},
            onSelectionChanged: _isSaving
                ? null
                : (selection) => setState(() => _tingiMode = selection.first),
          ),
          if (_tingiMode != TingiMode.none) ...[
            const SizedBox(height: AppSpacing.md),
            TextFormField(
              controller: _packagedSizeController,
              enabled: !_isSaving,
              style: const TextStyle(
                fontFeatures: [FontFeature.tabularFigures()],
                fontWeight: FontWeight.w600,
              ),
              decoration: _buildInputDecoration(
                label: 'Pack size (e.g. 50 for 50kg)',
                prefixIcon: Icons.inventory_2_outlined,
              ),
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              validator: (value) {
                if (_tingiMode == TingiMode.none) return null;
                final parsed = double.tryParse(value?.trim() ?? '');
                if (parsed == null || parsed <= 0) {
                  return 'Enter pack size > 0';
                }
                return null;
              },
            ),
          ],
          if (_tingiMode == TingiMode.increment) ...[
            const SizedBox(height: AppSpacing.sm),
            TextFormField(
              controller: _incrementStepController,
              enabled: !_isSaving,
              style: const TextStyle(
                fontFeatures: [FontFeature.tabularFigures()],
                fontWeight: FontWeight.w600,
              ),
              decoration: _buildInputDecoration(
                label: 'Increment step (e.g. 0.5 or 1.0)',
                prefixIcon: Icons.add_road_rounded,
              ),
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              validator: (value) {
                if (_tingiMode != TingiMode.increment) return null;
                final parsed = double.tryParse(value?.trim() ?? '');
                if (parsed == null || parsed <= 0) {
                  return 'Enter step > 0';
                }
                return null;
              },
            ),
          ],
          if (_tingiMode == TingiMode.fixedSizes) ...[
            const SizedBox(height: AppSpacing.md),
            const Text(
              'Portion sizes (e.g. 0.25, 0.5, 1):',
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            for (var i = 0; i < _sizeRows.length; i++) ...[
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Row(
                  children: [
                    Expanded(
                      child: TextFormField(
                        controller: _sizeRows[i].controller,
                        enabled: !_isSaving,
                        style: const TextStyle(
                          fontFeatures: [FontFeature.tabularFigures()],
                          fontWeight: FontWeight.w600,
                        ),
                        decoration: _buildInputDecoration(
                          label: 'Size ${i + 1}',
                          prefixIcon: Icons.straighten_rounded,
                        ),
                        keyboardType: const TextInputType.numberWithOptions(
                            decimal: true),
                      ),
                    ),
                    if (_sizeRows.length > 1) ...[
                      const SizedBox(width: AppSpacing.xs),
                      IconButton(
                        tooltip: 'Remove portion',
                        icon: const Icon(Icons.remove_circle_outline,
                            color: AppColors.error, size: 20),
                        onPressed: _isSaving
                            ? null
                            : () => setState(() {
                                  _sizeRows.removeAt(i).dispose();
                                }),
                      ),
                    ],
                  ],
                ),
              ),
            ],
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: _isSaving
                    ? null
                    : () => setState(() => _sizeRows.add(_SizeRow())),
                icon: const Icon(Icons.add, size: 16),
                label: const Text(
                  'Add portion size',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildBundleCard() {
    return _buildSubFormContainer(
      title: 'Initial Bundle Promotion Rule (Optional)',
      badgeTag: 'AUTO-PROMO',
      icon: Icons.inventory_rounded,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: TextFormField(
                  controller: _bundleTriggerQtyController,
                  enabled: !_isSaving,
                  style: const TextStyle(
                    fontFeatures: [FontFeature.tabularFigures()],
                    fontWeight: FontWeight.w600,
                  ),
                  decoration: _buildInputDecoration(
                    label: 'Trigger Qty',
                    hintText: 'e.g. 3',
                    prefixIcon: Icons.pin_outlined,
                  ),
                  keyboardType: TextInputType.number,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: TextFormField(
                  controller: _bundlePriceController,
                  enabled: !_isSaving,
                  style: const TextStyle(
                    fontFeatures: [FontFeature.tabularFigures()],
                    fontWeight: FontWeight.w700,
                    color: AppColors.brandPrimary,
                  ),
                  decoration: _buildInputDecoration(
                    label: 'Bundle Price',
                    hintText: 'e.g. 100',
                    prefixText: '₱ ',
                    prefixStyle: const TextStyle(
                      color: AppColors.brandPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  keyboardType: const TextInputType.numberWithOptions(
                      decimal: true),
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          TextFormField(
            controller: _bundleDescController,
            enabled: !_isSaving,
            decoration: _buildInputDecoration(
              label: 'Rule description (optional)',
              hintText: 'e.g. Buy 3 for ₱100 Special',
              prefixIcon: Icons.description_outlined,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildServiceCard() {
    final quickDurations = [15, 30, 45, 60, 90, 120];

    return _buildSubFormContainer(
      title: 'Service Appointment Duration',
      badgeTag: 'APPOINTMENT',
      icon: Icons.schedule_rounded,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextFormField(
            controller: _serviceDurationController,
            enabled: !_isSaving,
            style: const TextStyle(
              fontFeatures: [FontFeature.tabularFigures()],
              fontWeight: FontWeight.w600,
            ),
            decoration: _buildInputDecoration(
              label: 'Duration in minutes',
              hintText: 'e.g. 30',
              prefixIcon: Icons.timer_outlined,
              suffixIcon: const Padding(
                padding: EdgeInsets.symmetric(horizontal: AppSpacing.sm, vertical: 12),
                child: Text('mins', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 12, color: AppColors.textSecondary)),
              ),
            ),
            keyboardType: TextInputType.number,
            validator: (value) {
              if (_pricingType != PricingType.service) return null;
              final parsed = int.tryParse(value?.trim() ?? '');
              if (parsed == null || parsed <= 0) {
                return 'Enter duration greater than 0';
              }
              return null;
            },
          ),
          const SizedBox(height: AppSpacing.sm),
          Row(
            children: [
              Text(
                'Quick presets: ',
                style: TextStyle(
                  fontSize: 11,
                  color: AppColors.textSecondary.withValues(alpha: 0.8),
                  fontWeight: FontWeight.w500,
                ),
              ),
              const SizedBox(width: AppSpacing.xs),
              Expanded(
                child: Wrap(
                  spacing: 6,
                  runSpacing: 4,
                  children: quickDurations.map((mins) {
                    final isSelected = _serviceDurationController.text == mins.toString();
                    return ActionChip(
                      label: Text(
                        '$mins m',
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                          color: isSelected ? AppColors.brandPrimary : AppColors.textSecondary,
                        ),
                      ),
                      visualDensity: VisualDensity.compact,
                      backgroundColor: isSelected
                          ? AppColors.brandPrimaryContainer
                          : AppColors.surface,
                      side: BorderSide(
                        color: isSelected
                            ? AppColors.brandPrimary
                            : AppColors.border,
                      ),
                      padding: EdgeInsets.zero,
                      onPressed: _isSaving
                          ? null
                          : () {
                              _serviceDurationController.text = mins.toString();
                              setState(() {});
                            },
                    );
                  }).toList(),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildVariantMatrixCard() {
    return _buildSubFormContainer(
      title: 'Initial Variant Configuration (Optional)',
      badgeTag: 'MATRIX',
      icon: Icons.tune_rounded,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (var i = 0; i < _variantAttributes.length; i++) ...[
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Row(
                children: [
                  Expanded(
                    child: TextFormField(
                      controller: _variantAttributes[i].keyController,
                      enabled: !_isSaving,
                      decoration: _buildInputDecoration(
                        label: 'Attribute (e.g. Size)',
                        prefixIcon: Icons.label_outline_rounded,
                      ),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Expanded(
                    child: TextFormField(
                      controller: _variantAttributes[i].valueController,
                      enabled: !_isSaving,
                      decoration: _buildInputDecoration(
                        label: 'Value (e.g. Medium)',
                        prefixIcon: Icons.check_circle_outline_rounded,
                      ),
                    ),
                  ),
                  if (_variantAttributes.length > 1) ...[
                    const SizedBox(width: AppSpacing.xs),
                    IconButton(
                      tooltip: 'Remove attribute',
                      icon: const Icon(Icons.remove_circle_outline,
                          color: AppColors.error, size: 20),
                      onPressed: _isSaving
                          ? null
                          : () => setState(() {
                                _variantAttributes.removeAt(i).dispose();
                              }),
                    ),
                  ],
                ],
              ),
            ),
          ],
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              onPressed: _isSaving
                  ? null
                  : () => setState(() => _variantAttributes.add(_AttributePair())),
              icon: const Icon(Icons.add, size: 16),
              label: const Text(
                'Add attribute',
                style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
              ),
            ),
          ),
          const SizedBox(height: AppSpacing.xs),
          Row(
            children: [
              Expanded(
                child: TextFormField(
                  controller: _variantSkuController,
                  enabled: !_isSaving,
                  decoration: _buildInputDecoration(
                    label: 'Variant SKU (opt)',
                    hintText: 'e.g. VAR-01',
                    prefixIcon: Icons.tag_rounded,
                  ),
                ),
              ),
              const SizedBox(width: AppSpacing.xs),
              Expanded(
                child: TextFormField(
                  controller: _variantPriceOverrideController,
                  enabled: !_isSaving,
                  style: const TextStyle(
                    fontFeatures: [FontFeature.tabularFigures()],
                    fontWeight: FontWeight.w600,
                  ),
                  decoration: _buildInputDecoration(
                    label: 'Price Override (opt)',
                    prefixText: '₱ ',
                    prefixStyle: const TextStyle(
                      color: AppColors.brandPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  keyboardType: const TextInputType.numberWithOptions(
                      decimal: true),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildComboCard(AsyncValue<List<Category>> categoriesAsync) {
    return _buildSubFormContainer(
      title: 'Initial Combo Slot (Optional)',
      badgeTag: 'COMBO PACK',
      icon: Icons.fastfood_rounded,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextFormField(
            controller: _comboSlotLabelController,
            enabled: !_isSaving,
            decoration: _buildInputDecoration(
              label: 'Slot label (e.g. Choose a Drink)',
              prefixIcon: Icons.layers_outlined,
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          categoriesAsync.when(
            loading: () => const LinearProgressIndicator(),
            error: (e, s) => const SizedBox.shrink(),
            data: (categories) => DropdownButtonFormField<Category?>(
              value: _comboCategory,
              isExpanded: true,
              decoration: _buildInputDecoration(
                label: 'Component Category',
                prefixIcon: Icons.category_outlined,
              ),
              items: [
                const DropdownMenuItem<Category?>(
                  value: null,
                  child: Text('Select category for slot...'),
                ),
                ...categories.map(
                  (c) => DropdownMenuItem(value: c, child: Text(c.name)),
                ),
              ],
              onChanged: _isSaving
                  ? null
                  : (value) => setState(() => _comboCategory = value),
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          Row(
            children: [
              Expanded(
                child: TextFormField(
                  controller: _comboQtyController,
                  enabled: !_isSaving,
                  style: const TextStyle(
                    fontFeatures: [FontFeature.tabularFigures()],
                    fontWeight: FontWeight.w600,
                  ),
                  decoration: _buildInputDecoration(
                    label: 'Quantity',
                    prefixIcon: Icons.numbers_rounded,
                  ),
                  keyboardType: TextInputType.number,
                ),
              ),
              const SizedBox(width: AppSpacing.xs),
              Expanded(
                child: TextFormField(
                  controller: _comboUpchargeController,
                  enabled: !_isSaving,
                  style: const TextStyle(
                    fontFeatures: [FontFeature.tabularFigures()],
                    fontWeight: FontWeight.w600,
                  ),
                  decoration: _buildInputDecoration(
                    label: 'Upcharge (opt)',
                    prefixText: '₱ ',
                    prefixStyle: const TextStyle(
                      color: AppColors.brandPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  keyboardType: const TextInputType.numberWithOptions(
                      decimal: true),
                ),
              ),
            ],
          ),
        ],
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
