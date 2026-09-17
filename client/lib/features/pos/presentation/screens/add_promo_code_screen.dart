import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/promo_code_models.dart';
import '../providers/promo_code_providers.dart';

class AddPromoCodeScreen extends ConsumerStatefulWidget {
  const AddPromoCodeScreen({super.key});

  @override
  ConsumerState<AddPromoCodeScreen> createState() => _AddPromoCodeScreenState();
}

class _AddPromoCodeScreenState extends ConsumerState<AddPromoCodeScreen> {
  final _formKey = GlobalKey<FormState>();
  final _codeController = TextEditingController();
  final _valueController = TextEditingController();
  PromoDiscountType _discountType = PromoDiscountType.percentage;

  @override
  void dispose() {
    _codeController.dispose();
    _valueController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(createPromoCodeControllerProvider.notifier);
    final succeeded = await controller.create(
      CreatePromoCodeRequest(
        code: _codeController.text.trim(),
        discountType: _discountType,
        discountValue: double.parse(_valueController.text.trim()),
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
    final createState = ref.watch(createPromoCodeControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createPromoCodeControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Add Promo Code'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
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
                          controller: _codeController,
                          enabled: !isLoading,
                          textCapitalization: TextCapitalization.characters,
                          decoration: InputDecoration(
                            labelText: 'Code (e.g. SAVE10)',
                            isDense: true,
                            hintText: 'PROMOCODE',
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
                        const SizedBox(height: AppSpacing.md),
                        SegmentedButton<PromoDiscountType>(
                          segments: const [
                            ButtonSegment(
                              value: PromoDiscountType.percentage,
                              label: Text('% off'),
                            ),
                            ButtonSegment(
                              value: PromoDiscountType.fixedAmount,
                              label: Text('₱ off'),
                            ),
                          ],
                          selected: {_discountType},
                          onSelectionChanged:
                              isLoading
                                  ? null
                                  : (selection) => setState(
                                    () => _discountType = selection.first,
                                  ),
                        ),
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _valueController,
                          enabled: !isLoading,
                          style: const TextStyle(
                            fontFeatures: [FontFeature.tabularFigures()],
                            fontWeight: FontWeight.w600,
                          ),
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                          ),
                          decoration: InputDecoration(
                            labelText:
                                _discountType == PromoDiscountType.percentage
                                    ? 'Percent off'
                                    : 'Amount off',
                            isDense: true,
                            hintText: '0',
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
                          validator: (value) {
                            final parsed = double.tryParse(value?.trim() ?? '');
                            if (parsed == null || parsed <= 0) {
                              return 'Enter a value greater than zero';
                            }
                            if (_discountType == PromoDiscountType.percentage &&
                                parsed > 100) {
                              return 'Can\'t exceed 100%';
                            }
                            return null;
                          },
                        ),
                        if (failure != null) ...[
                          const SizedBox(height: AppSpacing.md),
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
                                    : const Text(
                                      'Add Promo Code',
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
