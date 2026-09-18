import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/item_promo_models.dart';
import '../../domain/promo_code_models.dart';
import '../providers/item_promo_providers.dart';
import 'item_picker_field.dart';
import 'promo_datetime_field.dart';

/// Create/edit form for a per-item discount (percent off / fixed amount off
/// / fixed override price) automatic promo rule. Pass [rule] to edit an
/// existing one.
class AddEditItemDiscountPromoScreen extends ConsumerStatefulWidget {
  const AddEditItemDiscountPromoScreen({super.key, this.rule});

  final ItemDiscountPromoRule? rule;

  @override
  ConsumerState<AddEditItemDiscountPromoScreen> createState() =>
      _AddEditItemDiscountPromoScreenState();
}

class _AddEditItemDiscountPromoScreenState
    extends ConsumerState<AddEditItemDiscountPromoScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _valueController;
  String? _itemId;
  PromoDiscountType _discountType = PromoDiscountType.percentage;
  DateTime? _startsAt;
  DateTime? _endsAt;
  bool _isActive = true;

  bool get _isEditing => widget.rule != null;

  @override
  void initState() {
    super.initState();
    final rule = widget.rule;
    _nameController = TextEditingController(text: rule?.name ?? '');
    _valueController = TextEditingController(
      text: rule == null ? '' : rule.discountValue.toStringAsFixed(2),
    );
    _itemId = rule?.itemId;
    _discountType = rule?.discountType ?? PromoDiscountType.percentage;
    _startsAt = rule?.startsAt;
    _endsAt = rule?.endsAt;
    _isActive = rule?.isActive ?? true;
  }

  @override
  void dispose() {
    _nameController.dispose();
    _valueController.dispose();
    super.dispose();
  }

  String get _valueLabel {
    switch (_discountType) {
      case PromoDiscountType.percentage:
        return 'Percent off (%)';
      case PromoDiscountType.fixedAmount:
        return 'Amount off (₱)';
      case PromoDiscountType.fixedPrice:
        return 'Fixed price (₱)';
    }
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    if (_itemId == null) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Select an item')));
      return;
    }

    final controller = ref.read(
      itemDiscountPromoRuleControllerProvider.notifier,
    );
    final name = _nameController.text.trim();
    final discountValue = double.parse(_valueController.text.trim());

    final succeeded =
        _isEditing
            ? await controller.edit(
              widget.rule!.id,
              UpdateItemDiscountPromoRuleRequest(
                name: name,
                itemId: _itemId!,
                discountType: _discountType,
                discountValue: discountValue,
                startsAt: _startsAt,
                endsAt: _endsAt,
                isActive: _isActive,
              ),
            )
            : await controller.create(
              CreateItemDiscountPromoRuleRequest(
                name: name,
                itemId: _itemId!,
                discountType: _discountType,
                discountValue: discountValue,
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
    final controllerState = ref.watch(itemDiscountPromoRuleControllerProvider);
    final isLoading = controllerState.isLoading;
    final failure =
        ref
            .read(itemDiscountPromoRuleControllerProvider.notifier)
            .currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(_isEditing ? 'Edit Item Discount' : 'Add Item Discount'),
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
                        hintText: 'e.g. 20% off Coffee this weekend',
                      ),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
                    ),
                    const SizedBox(height: AppSpacing.md),
                    ItemPickerField(
                      label: 'Item',
                      selectedItemId: _itemId,
                      onChanged: (item) => setState(() => _itemId = item.id),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    DropdownButtonFormField<PromoDiscountType>(
                      value: _discountType,
                      decoration: const InputDecoration(
                        labelText: 'Discount type',
                        isDense: true,
                      ),
                      items: const [
                        DropdownMenuItem(
                          value: PromoDiscountType.percentage,
                          child: Text('Percentage'),
                        ),
                        DropdownMenuItem(
                          value: PromoDiscountType.fixedAmount,
                          child: Text('Fixed Amount'),
                        ),
                        DropdownMenuItem(
                          value: PromoDiscountType.fixedPrice,
                          child: Text('Fixed Price'),
                        ),
                      ],
                      onChanged:
                          isLoading
                              ? null
                              : (value) =>
                                  setState(() => _discountType = value!),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    TextFormField(
                      controller: _valueController,
                      enabled: !isLoading,
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      decoration: InputDecoration(
                        labelText: _valueLabel,
                        isDense: true,
                      ),
                      validator: (value) {
                        final parsed = double.tryParse(value?.trim() ?? '');
                        if (parsed == null || parsed <= 0) {
                          return 'Enter a value greater than zero';
                        }
                        if (_discountType == PromoDiscountType.percentage &&
                            parsed > 100) {
                          return "Can't exceed 100%";
                        }
                        return null;
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
