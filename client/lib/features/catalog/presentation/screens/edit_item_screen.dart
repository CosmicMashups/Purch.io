import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/hardware/barcode_scanner_screen.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/image_upload_field.dart';
import '../../../../core/widgets/status_badge.dart';
import '../../../inventory/presentation/screens/recipe_editor_screen.dart';
import '../../../onboarding/presentation/providers/onboarding_providers.dart';
import '../../domain/item_models.dart';
import '../../domain/pricing_type.dart';
import '../providers/catalog_providers.dart';

class EditItemScreen extends ConsumerStatefulWidget {
  const EditItemScreen({super.key, required this.item});

  final Item item;

  @override
  ConsumerState<EditItemScreen> createState() => _EditItemScreenState();
}

class _EditItemScreenState extends ConsumerState<EditItemScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _skuController;
  late final TextEditingController _barcodeController;
  late final TextEditingController _priceController;
  late final TextEditingController _imageUrlController;
  String? _selectedCategoryId;
  String? _selectedDepartmentId;
  late bool _isActive;

  @override
  void initState() {
    super.initState();
    final item = widget.item;
    _nameController = TextEditingController(text: item.name);
    _skuController = TextEditingController(text: item.sku ?? '');
    _barcodeController = TextEditingController(text: item.barcode ?? '');
    _priceController = TextEditingController(
      text: item.basePrice.toStringAsFixed(2),
    );
    _imageUrlController = TextEditingController(text: item.imageUrl ?? '');
    _selectedCategoryId = item.categoryId;
    _selectedDepartmentId = item.departmentId;
    _isActive = item.isActive;
  }

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
      setState(() {
        _barcodeController.text = scanned;
      });
    }
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final price = double.tryParse(_priceController.text.trim());
    if (price == null) return;

    final controller = ref.read(updateItemControllerProvider.notifier);
    final succeeded = await controller.updateItem(
      widget.item.id,
      UpdateItemRequest(
        name: _nameController.text.trim(),
        sku: _skuController.text.trim().isEmpty
            ? null
            : _skuController.text.trim(),
        barcode: _barcodeController.text.trim().isEmpty
            ? null
            : _barcodeController.text.trim(),
        categoryId: _selectedCategoryId,
        basePrice: price,
        imageUrl: _imageUrlController.text.trim().isEmpty
            ? null
            : _imageUrlController.text.trim(),
        isActive: _isActive,
        departmentId: _selectedDepartmentId,
      ),
    );

    if (!mounted) return;

    if (succeeded) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: AppColors.success,
          content: Text('Item "${_nameController.text.trim()}" updated successfully'),
        ),
      );
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final updateState = ref.watch(updateItemControllerProvider);
    final isLoading = updateState.isLoading;
    final failure =
        ref.read(updateItemControllerProvider.notifier).currentFailure;

    final categoriesAsync = ref.watch(categoryListProvider);
    final departmentsAsync = ref.watch(allDepartmentsProvider);
    final useSeparateInventoryTracking = ref
        .watch(tenantSettingsNotifierProvider)
        .maybeWhen(
          data: (settings) => settings.useSeparateInventoryTracking,
          orElse: () => false,
        );

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Edit Item'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: AppSpacing.lg),
            child: Center(
              child: _isActive ? StatusBadge.active() : StatusBadge.inactive(),
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.lg,
                vertical: AppSpacing.xl,
              ),
              child: Container(
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: AppRadius.lgBorder,
                  border: Border.all(color: AppColors.border),
                  boxShadow: AppShadows.card,
                ),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.xl),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        // Header with pricing type pill and stock level
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Container(
                              width: 52,
                              height: 52,
                              decoration: BoxDecoration(
                                color: _isActive
                                    ? AppColors.brandPrimaryContainer
                                    : AppColors.neutralContainer,
                                borderRadius: AppRadius.mdBorder,
                                border: Border.all(
                                  color: _isActive
                                      ? AppColors.brandPrimary.withValues(alpha: 0.2)
                                      : AppColors.neutralBorder,
                                ),
                              ),
                              child: Icon(
                                _isActive
                                    ? Icons.inventory_2_rounded
                                    : Icons.inventory_2_outlined,
                                color: _isActive
                                    ? AppColors.brandPrimary
                                    : AppColors.textMuted,
                                size: 28,
                              ),
                            ),
                            const SizedBox(width: AppSpacing.md),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    widget.item.name,
                                    style: const TextStyle(
                                      fontSize: 18,
                                      fontWeight: FontWeight.w700,
                                      color: AppColors.textPrimary,
                                      letterSpacing: -0.2,
                                    ),
                                  ),
                                  const SizedBox(height: 4),
                                  Wrap(
                                    spacing: 6,
                                    runSpacing: 4,
                                    children: [
                                      StatusBadge(
                                        label: widget.item.pricingType.label,
                                        type: StatusBadgeType.info,
                                        isSmall: true,
                                      ),
                                      StatusBadge.stockLevel(
                                        stockOnHand: widget.item.stockOnHand,
                                        lowStockThreshold:
                                            widget.item.lowStockThreshold,
                                        isSmall: true,
                                      ),
                                    ],
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: AppSpacing.xl),

                        if (failure != null) ...[
                          Container(
                            padding: const EdgeInsets.all(AppSpacing.md),
                            decoration: BoxDecoration(
                              color: AppColors.errorContainer,
                              borderRadius: AppRadius.mdBorder,
                              border: Border.all(color: AppColors.errorBorder),
                            ),
                            child: Row(
                              children: [
                                const Icon(
                                  Icons.error_outline_rounded,
                                  color: AppColors.error,
                                  size: 20,
                                ),
                                const SizedBox(width: AppSpacing.sm),
                                Expanded(
                                  child: Text(
                                    describeError(failure),
                                    style: const TextStyle(
                                      color: AppColors.onErrorContainer,
                                      fontSize: 13,
                                      fontWeight: FontWeight.w500,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: AppSpacing.lg),
                        ],

                        // Item Name
                        TextFormField(
                          controller: _nameController,
                          enabled: !isLoading,
                          textInputAction: TextInputAction.next,
                          decoration: const InputDecoration(
                            labelText: 'Item name',
                            hintText: 'e.g. Bottled Water 500ml',
                            prefixIcon: Icon(Icons.label_outline_rounded),
                          ),
                          validator: (value) {
                            final trimmed = value?.trim() ?? '';
                            if (trimmed.isEmpty) {
                              return 'Item name is required';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: AppSpacing.lg),

                        // Base Price
                        TextFormField(
                          controller: _priceController,
                          enabled: !isLoading,
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                          ),
                          textInputAction: TextInputAction.next,
                          decoration: const InputDecoration(
                            labelText: 'Base Price (₱)',
                            hintText: '0.00',
                            prefixIcon: Icon(Icons.payments_outlined),
                          ),
                          validator: (value) {
                            final trimmed = value?.trim() ?? '';
                            if (trimmed.isEmpty) {
                              return 'Price is required';
                            }
                            final parsed = double.tryParse(trimmed);
                            if (parsed == null) {
                              return 'Enter a valid number';
                            }
                            if (parsed < 0) {
                              return 'Price cannot be negative';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: AppSpacing.lg),

                        // Barcode & SKU Row
                        Row(
                          children: [
                            Expanded(
                              child: TextFormField(
                                controller: _barcodeController,
                                enabled: !isLoading,
                                textInputAction: TextInputAction.next,
                                decoration: InputDecoration(
                                  labelText: 'Barcode',
                                  hintText: 'UPC / EAN code',
                                  prefixIcon: const Icon(Icons.qr_code_2_rounded),
                                  suffixIcon: IconButton(
                                    icon: const Icon(Icons.qr_code_scanner),
                                    tooltip: 'Scan barcode',
                                    onPressed: isLoading ? null : _scanBarcode,
                                  ),
                                ),
                              ),
                            ),
                            const SizedBox(width: AppSpacing.md),
                            Expanded(
                              child: TextFormField(
                                controller: _skuController,
                                enabled: !isLoading,
                                textInputAction: TextInputAction.next,
                                decoration: const InputDecoration(
                                  labelText: 'SKU',
                                  hintText: 'Internal SKU code',
                                  prefixIcon: Icon(Icons.tag_rounded),
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: AppSpacing.lg),

                        // Category Dropdown
                        categoriesAsync.maybeWhen(
                          data: (categories) {
                            return DropdownButtonFormField<String?>(
                              value: categories.any((c) => c.id == _selectedCategoryId)
                                  ? _selectedCategoryId
                                  : null,
                              decoration: const InputDecoration(
                                labelText: 'Category',
                                prefixIcon: Icon(Icons.category_outlined),
                              ),
                              items: [
                                const DropdownMenuItem<String?>(
                                  value: null,
                                  child: Text('None (Uncategorized)'),
                                ),
                                ...categories.map(
                                  (category) => DropdownMenuItem<String?>(
                                    value: category.id,
                                    child: Text(category.name),
                                  ),
                                ),
                              ],
                              onChanged: isLoading
                                  ? null
                                  : (value) => setState(
                                        () => _selectedCategoryId = value,
                                      ),
                            );
                          },
                          orElse: () => const SizedBox.shrink(),
                        ),
                        const SizedBox(height: AppSpacing.lg),

                        // Department Dropdown
                        departmentsAsync.maybeWhen(
                          data: (departments) {
                            if (departments.isEmpty) return const SizedBox.shrink();
                            return Column(
                              children: [
                                DropdownButtonFormField<String?>(
                                  value: departments.any(
                                          (d) => d.id == _selectedDepartmentId)
                                      ? _selectedDepartmentId
                                      : null,
                                  decoration: const InputDecoration(
                                    labelText: 'Department / Concessionaire',
                                    prefixIcon: Icon(Icons.store_outlined),
                                  ),
                                  items: [
                                    const DropdownMenuItem<String?>(
                                      value: null,
                                      child: Text('None'),
                                    ),
                                    ...departments.map(
                                      (department) => DropdownMenuItem<String?>(
                                        value: department.id,
                                        child: Text(department.name),
                                      ),
                                    ),
                                  ],
                                  onChanged: isLoading
                                      ? null
                                      : (value) => setState(
                                            () => _selectedDepartmentId = value,
                                          ),
                                ),
                                const SizedBox(height: AppSpacing.lg),
                              ],
                            );
                          },
                          orElse: () => const SizedBox.shrink(),
                        ),

                        if (useSeparateInventoryTracking) ...[
                          OutlinedButton.icon(
                            onPressed: isLoading
                                ? null
                                : () => Navigator.of(context).push<void>(
                                      MaterialPageRoute(
                                        builder: (_) => RecipeEditorScreen(
                                          itemId: widget.item.id,
                                          itemName: widget.item.name,
                                        ),
                                      ),
                                    ),
                            icon: const Icon(Icons.receipt_long_outlined),
                            label: const Text('Manage Recipe'),
                            style: OutlinedButton.styleFrom(
                              foregroundColor: AppColors.brandPrimary,
                              side: const BorderSide(color: AppColors.border),
                              minimumSize: const Size.fromHeight(44),
                              shape: const RoundedRectangleBorder(
                                borderRadius: AppRadius.mdBorder,
                              ),
                            ),
                          ),
                          const SizedBox(height: AppSpacing.lg),
                        ],

                        // Active State Switch
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: AppSpacing.md,
                            vertical: AppSpacing.sm,
                          ),
                          decoration: BoxDecoration(
                            color: _isActive
                                ? AppColors.successContainer.withValues(alpha: 0.4)
                                : AppColors.neutralContainer,
                            borderRadius: AppRadius.mdBorder,
                            border: Border.all(
                              color: _isActive
                                  ? AppColors.successBorder
                                  : AppColors.neutralBorder,
                            ),
                          ),
                          child: Row(
                            children: [
                              Icon(
                                _isActive
                                    ? Icons.check_circle_rounded
                                    : Icons.pause_circle_outline_rounded,
                                color: _isActive
                                    ? AppColors.success
                                    : AppColors.textMuted,
                                size: 24,
                              ),
                              const SizedBox(width: AppSpacing.md),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      _isActive
                                          ? 'Item is Active'
                                          : 'Item is Inactive',
                                      style: TextStyle(
                                        fontSize: 15,
                                        fontWeight: FontWeight.w600,
                                        color: _isActive
                                          ? AppColors.onSuccessContainer
                                          : AppColors.textSecondary,
                                      ),
                                    ),
                                    Text(
                                      _isActive
                                          ? 'Visible on POS cashier & customer kiosk'
                                          : 'Hidden from sales screens',
                                      style: const TextStyle(
                                        fontSize: 12,
                                        color: AppColors.textSecondary,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              Switch.adaptive(
                                value: _isActive,
                                activeColor: AppColors.success,
                                onChanged: isLoading
                                    ? null
                                    : (val) => setState(() => _isActive = val),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: AppSpacing.lg),

                        // Image upload field
                        ImageUploadField(
                          controller: _imageUrlController,
                          label: 'Item image',
                          hintText: 'Upload or enter an image URL',
                          enabled: !isLoading,
                        ),
                        const SizedBox(height: AppSpacing.xl),

                        // Save Button
                        FilledButton(
                          onPressed: isLoading ? null : _submit,
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            foregroundColor: AppColors.onBrandPrimary,
                            minimumSize: const Size.fromHeight(48),
                            shape: const RoundedRectangleBorder(
                              borderRadius: AppRadius.mdBorder,
                            ),
                          ),
                          child: isLoading
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : const Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    Icon(Icons.check_rounded, size: 18),
                                    SizedBox(width: 8),
                                    Text(
                                      'Save Changes',
                                      style: TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                  ],
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
}
