import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
      appBar: AppBar(title: const Text('Add Promo Code')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(32),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextFormField(
                      controller: _codeController,
                      enabled: !isLoading,
                      textCapitalization: TextCapitalization.characters,
                      decoration: const InputDecoration(
                        labelText: 'Code (e.g. SAVE10)',
                        border: OutlineInputBorder(),
                      ),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
                    ),
                    const SizedBox(height: 16),
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
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _valueController,
                      enabled: !isLoading,
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      decoration: InputDecoration(
                        labelText:
                            _discountType == PromoDiscountType.percentage
                                ? 'Percent off'
                                : 'Amount off',
                        border: const OutlineInputBorder(),
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
                                : const Text('Add Promo Code'),
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
