import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/item_variant_models.dart';
import '../providers/catalog_providers.dart';

class AddVariantScreen extends ConsumerStatefulWidget {
  const AddVariantScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  ConsumerState<AddVariantScreen> createState() => _AddVariantScreenState();
}

class _AttributeRow {
  _AttributeRow()
    : keyController = TextEditingController(),
      valueController = TextEditingController();

  final TextEditingController keyController;
  final TextEditingController valueController;

  void dispose() {
    keyController.dispose();
    valueController.dispose();
  }
}

class _AddVariantScreenState extends ConsumerState<AddVariantScreen> {
  final _formKey = GlobalKey<FormState>();
  final _skuController = TextEditingController();
  final _priceOverrideController = TextEditingController();
  final List<_AttributeRow> _attributeRows = [_AttributeRow()];

  @override
  void dispose() {
    _skuController.dispose();
    _priceOverrideController.dispose();
    for (final row in _attributeRows) {
      row.dispose();
    }
    super.dispose();
  }

  void _addAttributeRow() {
    setState(() => _attributeRows.add(_AttributeRow()));
  }

  void _removeAttributeRow(int index) {
    setState(() {
      _attributeRows.removeAt(index).dispose();
    });
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final attributes = <String, String>{
      for (final row in _attributeRows)
        if (row.keyController.text.trim().isNotEmpty)
          row.keyController.text.trim(): row.valueController.text.trim(),
    };

    if (attributes.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Add at least one attribute.')),
      );
      return;
    }

    final controller = ref.read(
      createVariantControllerProvider(widget.itemId).notifier,
    );
    final succeeded = await controller.create(
      CreateItemVariantRequest(
        attributes: attributes,
        sku:
            _skuController.text.trim().isEmpty
                ? null
                : _skuController.text.trim(),
        priceOverride:
            _priceOverrideController.text.trim().isEmpty
                ? null
                : double.parse(_priceOverrideController.text.trim()),
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
    final createState = ref.watch(
      createVariantControllerProvider(widget.itemId),
    );
    final isLoading = createState.isLoading;
    final failure =
        ref
            .read(createVariantControllerProvider(widget.itemId).notifier)
            .currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Add Variant: ${widget.itemName}'),
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
                                Icons.style_outlined,
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
                                    'New Item Variant',
                                    style: Theme.of(context).textTheme.titleLarge
                                        ?.copyWith(fontWeight: FontWeight.w700),
                                  ),
                                  const SizedBox(height: 2),
                                  Text(
                                    'Define options (e.g. Size, Color) for ${widget.itemName}',
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
                        Text(
                          'Attributes',
                          style: Theme.of(context).textTheme.titleSmall
                              ?.copyWith(fontWeight: FontWeight.w700),
                        ),
                        const SizedBox(height: AppSpacing.sm),
                        for (
                          var index = 0;
                          index < _attributeRows.length;
                          index++
                        ) ...[
                          Row(
                            children: [
                              Expanded(
                                child: TextFormField(
                                  controller: _attributeRows[index].keyController,
                                  enabled: !isLoading,
                                  decoration: InputDecoration(
                                    labelText: 'Attribute (e.g. Size)',
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
                              ),
                              const SizedBox(width: AppSpacing.sm),
                              Expanded(
                                child: TextFormField(
                                  controller: _attributeRows[index].valueController,
                                  enabled: !isLoading,
                                  decoration: InputDecoration(
                                    labelText: 'Value (e.g. Large)',
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
                              ),
                              IconButton(
                                onPressed:
                                    isLoading || _attributeRows.length == 1
                                        ? null
                                        : () => _removeAttributeRow(index),
                                icon: const Icon(
                                  Icons.remove_circle_outline,
                                  color: AppColors.accentWarm,
                                ),
                                tooltip: 'Remove attribute',
                              ),
                            ],
                          ),
                          const SizedBox(height: AppSpacing.sm),
                        ],
                        Align(
                          alignment: Alignment.centerLeft,
                          child: TextButton.icon(
                            onPressed: isLoading ? null : _addAttributeRow,
                            icon: const Icon(Icons.add, size: 18),
                            label: const Text('Add attribute'),
                            style: TextButton.styleFrom(
                              foregroundColor: AppColors.brandPrimary,
                            ),
                          ),
                        ),
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _skuController,
                          enabled: !isLoading,
                          decoration: InputDecoration(
                            labelText: 'SKU (optional)',
                            hintText: 'e.g. TSHIRT-LRG-RED',
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
                        const SizedBox(height: AppSpacing.lg),
                        TextFormField(
                          controller: _priceOverrideController,
                          enabled: !isLoading,
                          style: const TextStyle(
                            fontFeatures: [FontFeature.tabularFigures()],
                            fontWeight: FontWeight.w600,
                          ),
                          decoration: InputDecoration(
                            labelText: 'Price override (optional)',
                            hintText: 'Leave blank to use base price',
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
                              return 'Enter a valid price';
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
                                      'Add Variant',
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
