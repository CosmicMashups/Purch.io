import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/item_promo_models.dart';
import '../providers/item_promo_providers.dart';
import 'item_picker_field.dart';
import 'promo_datetime_field.dart';

/// Create/edit form for a Combo bundle ("item A + item B = fixed total")
/// automatic promo rule. Pass [rule] to edit an existing one.
class AddEditComboPromoScreen extends ConsumerStatefulWidget {
  const AddEditComboPromoScreen({super.key, this.rule});

  final ComboPromoRule? rule;

  @override
  ConsumerState<AddEditComboPromoScreen> createState() =>
      _AddEditComboPromoScreenState();
}

class _AddEditComboPromoScreenState
    extends ConsumerState<AddEditComboPromoScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _priceController;
  String? _itemAId;
  String? _itemBId;
  DateTime? _startsAt;
  DateTime? _endsAt;
  bool _isActive = true;

  bool get _isEditing => widget.rule != null;

  @override
  void initState() {
    super.initState();
    final rule = widget.rule;
    _nameController = TextEditingController(text: rule?.name ?? '');
    _priceController = TextEditingController(
      text: rule == null ? '' : rule.comboPrice.toStringAsFixed(2),
    );
    _itemAId = rule?.itemAId;
    _itemBId = rule?.itemBId;
    _startsAt = rule?.startsAt;
    _endsAt = rule?.endsAt;
    _isActive = rule?.isActive ?? true;
  }

  @override
  void dispose() {
    _nameController.dispose();
    _priceController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    if (_itemAId == null || _itemBId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Select both items in the bundle')),
      );
      return;
    }

    final controller = ref.read(comboPromoRuleControllerProvider.notifier);
    final name = _nameController.text.trim();
    final comboPrice = double.parse(_priceController.text.trim());

    final succeeded =
        _isEditing
            ? await controller.edit(
              widget.rule!.id,
              UpdateComboPromoRuleRequest(
                name: name,
                itemAId: _itemAId!,
                itemBId: _itemBId!,
                comboPrice: comboPrice,
                startsAt: _startsAt,
                endsAt: _endsAt,
                isActive: _isActive,
              ),
            )
            : await controller.create(
              CreateComboPromoRuleRequest(
                name: name,
                itemAId: _itemAId!,
                itemBId: _itemBId!,
                comboPrice: comboPrice,
                startsAt: _startsAt,
                endsAt: _endsAt,
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
    final controllerState = ref.watch(comboPromoRuleControllerProvider);
    final isLoading = controllerState.isLoading;
    final failure =
        ref.read(comboPromoRuleControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(_isEditing ? 'Edit Combo Deal' : 'Add Combo Deal'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextFormField(
                      controller: _nameController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Name',
                        isDense: true,
                        hintText: 'e.g. Burger + Fries Combo',
                      ),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
                    ),
                    const SizedBox(height: AppSpacing.md),
                    ItemPickerField(
                      label: 'Item A',
                      selectedItemId: _itemAId,
                      onChanged: (item) => setState(() => _itemAId = item.id),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    ItemPickerField(
                      label: 'Item B',
                      selectedItemId: _itemBId,
                      onChanged: (item) => setState(() => _itemBId = item.id),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    TextFormField(
                      controller: _priceController,
                      enabled: !isLoading,
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      decoration: const InputDecoration(
                        labelText: 'Combo price (₱)',
                        isDense: true,
                      ),
                      validator: (value) {
                        final parsed = double.tryParse(value?.trim() ?? '');
                        return (parsed == null || parsed <= 0)
                            ? 'Enter a value greater than zero'
                            : null;
                      },
                    ),
                    const SizedBox(height: AppSpacing.lg),
                    PromoDateTimeField(
                      label: 'Starts at',
                      value: _startsAt,
                      onChanged: (value) => setState(() => _startsAt = value),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    PromoDateTimeField(
                      label: 'Ends at',
                      value: _endsAt,
                      onChanged: (value) => setState(() => _endsAt = value),
                    ),
                    if (_isEditing) ...[
                      const SizedBox(height: AppSpacing.md),
                      SwitchListTile(
                        contentPadding: EdgeInsets.zero,
                        value: _isActive,
                        activeColor: AppColors.brandPrimary,
                        title: const Text('Active'),
                        onChanged:
                            (value) => setState(() => _isActive = value),
                      ),
                    ],
                    if (failure != null) ...[
                      const SizedBox(height: AppSpacing.md),
                      Container(
                        padding: const EdgeInsets.all(AppSpacing.md),
                        decoration: BoxDecoration(
                          color: AppColors.accentWarm.withValues(alpha: 0.1),
                          borderRadius: AppRadius.mdBorder,
                        ),
                        child: Text(
                          failure.message,
                          style: const TextStyle(color: AppColors.accentWarm),
                          textAlign: TextAlign.center,
                        ),
                      ),
                    ],
                    const SizedBox(height: AppSpacing.lg),
                    SizedBox(
                      height: 48,
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
                                : Text(_isEditing ? 'Save Changes' : 'Add Promo'),
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
