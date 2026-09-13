import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/category_models.dart';
import '../../domain/item_combo_component_models.dart';
import '../providers/catalog_providers.dart';
import '../../../../core/errors/failure.dart';

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
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Add Combo Slot: ${widget.itemName}'),
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
                horizontal: AppSpacing.lg,
                vertical: AppSpacing.xl,
              ),
              child: Card(
                elevation: 0,
                color: AppColors.surface,
                shape: RoundedRectangleBorder(
                  borderRadius: AppRadius.lgBorder,
                  side: const BorderSide(color: AppColors.border),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.xl),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Row(
                          children: [
                            Container(
                              padding: const EdgeInsets.all(AppSpacing.sm),
                              decoration: BoxDecoration(
                                color: AppColors.brandPrimaryContainer,
                                borderRadius: AppRadius.mdBorder,
                              ),
                              child: const Icon(
                                Icons.fastfood_outlined,
                                color: AppColors.brandPrimary,
                                size: 24,
                              ),
                            ),
                            const SizedBox(width: AppSpacing.md),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    'New Combo Slot',
                                    style: Theme.of(context).textTheme.titleLarge
                                        ?.copyWith(fontWeight: FontWeight.w700),
                                  ),
                                  const SizedBox(height: 2),
                                  Text(
                                    'Configure customizable meal slot for ${widget.itemName}',
                                    style: Theme.of(context).textTheme.bodySmall
                                        ?.copyWith(color: AppColors.textSecondary),
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: AppSpacing.xl),
                        TextFormField(
                          controller: _slotLabelController,
                          enabled: !isLoading,
                          decoration: InputDecoration(
                            labelText: 'Slot label (e.g. "Choose a Drink")',
                            hintText: 'e.g. Choose a Drink, Choose a Side',
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
                        const SizedBox(height: AppSpacing.lg),
                        categoriesAsync.when(
                          loading: () => const LinearProgressIndicator(),
                          error:
                              (error, stackTrace) =>
                                  Text('Could not load categories: ${describeError(error)}'),
                          data:
                              (categories) => DropdownButtonFormField<String>(
                                value: _selectedCategoryId,
                                decoration: InputDecoration(
                                  labelText: 'Category to choose from',
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
                        const SizedBox(height: AppSpacing.lg),
                        TextFormField(
                          controller: _quantityController,
                          enabled: !isLoading,
                          style: const TextStyle(
                            fontFeatures: [FontFeature.tabularFigures()],
                            fontWeight: FontWeight.w600,
                          ),
                          decoration: InputDecoration(
                            labelText: 'Quantity',
                            hintText: '1',
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
                          keyboardType: TextInputType.number,
                          validator: (value) {
                            final parsed = int.tryParse(value?.trim() ?? '');
                            if (parsed == null || parsed < 1) {
                              return 'Enter a quantity of 1 or more';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: AppSpacing.lg),
                        TextFormField(
                          controller: _upchargeController,
                          enabled: !isLoading,
                          style: const TextStyle(
                            fontFeatures: [FontFeature.tabularFigures()],
                            fontWeight: FontWeight.w600,
                          ),
                          decoration: InputDecoration(
                            labelText: 'Substitution upcharge (optional)',
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
                                      'Add Combo Slot',
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
}
