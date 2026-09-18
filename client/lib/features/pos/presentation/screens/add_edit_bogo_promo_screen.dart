import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/item_promo_models.dart';
import '../providers/item_promo_providers.dart';
import 'item_picker_field.dart';
import 'promo_datetime_field.dart';

/// Create/edit form for a BOGO ("Buy N, get M free") automatic promo rule.
/// Pass [rule] to edit an existing one, or omit it to create a new one.
class AddEditBogoPromoScreen extends ConsumerStatefulWidget {
  const AddEditBogoPromoScreen({super.key, this.rule});

  final BogoPromoRule? rule;

  @override
  ConsumerState<AddEditBogoPromoScreen> createState() =>
      _AddEditBogoPromoScreenState();
}

class _AddEditBogoPromoScreenState
    extends ConsumerState<AddEditBogoPromoScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _triggerQtyController;
  late final TextEditingController _freeQtyController;
  String? _triggerItemId;
  String? _freeItemId;
  DateTime? _startsAt;
  DateTime? _endsAt;
  bool _isActive = true;

  bool get _isEditing => widget.rule != null;

  @override
  void initState() {
    super.initState();
    final rule = widget.rule;
    _nameController = TextEditingController(text: rule?.name ?? '');
    _triggerQtyController = TextEditingController(
      text: (rule?.triggerQuantity ?? 1).toString(),
    );
    _freeQtyController = TextEditingController(
      text: (rule?.freeQuantity ?? 1).toString(),
    );
    _triggerItemId = rule?.triggerItemId;
    _freeItemId = rule?.freeItemId;
    _startsAt = rule?.startsAt;
    _endsAt = rule?.endsAt;
    _isActive = rule?.isActive ?? true;
  }

  @override
  void dispose() {
    _nameController.dispose();
    _triggerQtyController.dispose();
    _freeQtyController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    if (_triggerItemId == null || _freeItemId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Select both a trigger and a free item')),
      );
      return;
    }

    final controller = ref.read(bogoPromoRuleControllerProvider.notifier);
    final name = _nameController.text.trim();
    final triggerQuantity = int.parse(_triggerQtyController.text.trim());
    final freeQuantity = int.parse(_freeQtyController.text.trim());

    final succeeded =
        _isEditing
            ? await controller.edit(
              widget.rule!.id,
              UpdateBogoPromoRuleRequest(
                name: name,
                triggerItemId: _triggerItemId!,
                triggerQuantity: triggerQuantity,
                freeItemId: _freeItemId!,
                freeQuantity: freeQuantity,
                startsAt: _startsAt,
                endsAt: _endsAt,
                isActive: _isActive,
              ),
            )
            : await controller.create(
              CreateBogoPromoRuleRequest(
                name: name,
                triggerItemId: _triggerItemId!,
                triggerQuantity: triggerQuantity,
                freeItemId: _freeItemId!,
                freeQuantity: freeQuantity,
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
    final controllerState = ref.watch(bogoPromoRuleControllerProvider);
    final isLoading = controllerState.isLoading;
    final failure =
        ref.read(bogoPromoRuleControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(_isEditing ? 'Edit Buy 1 Take 1' : 'Add Buy 1 Take 1'),
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
                        hintText: 'e.g. Buy 1 Take 1 Iced Tea',
                      ),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
                    ),
                    const SizedBox(height: AppSpacing.md),
                    ItemPickerField(
                      label: 'Trigger item',
                      selectedItemId: _triggerItemId,
                      onChanged:
                          (item) => setState(() => _triggerItemId = item.id),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    TextFormField(
                      controller: _triggerQtyController,
                      enabled: !isLoading,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Trigger quantity',
                        isDense: true,
                      ),
                      validator: (value) {
                        final parsed = int.tryParse(value?.trim() ?? '');
                        return (parsed == null || parsed <= 0)
                            ? 'Enter a whole number greater than zero'
                            : null;
                      },
                    ),
                    const SizedBox(height: AppSpacing.lg),
                    ItemPickerField(
                      label: 'Free item',
                      selectedItemId: _freeItemId,
                      onChanged: (item) => setState(() => _freeItemId = item.id),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    TextFormField(
                      controller: _freeQtyController,
                      enabled: !isLoading,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Free quantity',
                        isDense: true,
                      ),
                      validator: (value) {
                        final parsed = int.tryParse(value?.trim() ?? '');
                        return (parsed == null || parsed <= 0)
                            ? 'Enter a whole number greater than zero'
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
