import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/credit_ledger_models.dart';
import '../providers/credit_ledger_providers.dart';

class AddCustomerCreditLedgerScreen extends ConsumerStatefulWidget {
  const AddCustomerCreditLedgerScreen({super.key});

  @override
  ConsumerState<AddCustomerCreditLedgerScreen> createState() =>
      _AddCustomerCreditLedgerScreenState();
}

class _AddCustomerCreditLedgerScreenState
    extends ConsumerState<AddCustomerCreditLedgerScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _addressController = TextEditingController();
  final _creditLimitController = TextEditingController();
  DateTime? _dueDate;

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    _addressController.dispose();
    _creditLimitController.dispose();
    super.dispose();
  }

  Future<void> _pickDueDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime.now().add(const Duration(days: 30)),
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 365 * 2)),
    );
    if (picked != null) {
      setState(() => _dueDate = picked);
    }
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(createCreditLedgerControllerProvider.notifier);
    final succeeded = await controller.create(
      CreateCustomerCreditLedgerRequest(
        customerFullName: _nameController.text.trim(),
        customerPhoneNumber: _phoneController.text.trim(),
        customerAddress:
            _addressController.text.trim().isEmpty
                ? null
                : _addressController.text.trim(),
        creditLimit: double.parse(_creditLimitController.text.trim()),
        dueDate: _dueDate,
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
    final createState = ref.watch(createCreditLedgerControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createCreditLedgerControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Add Customer Account'),
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
                          controller: _nameController,
                          enabled: !isLoading,
                          decoration: InputDecoration(
                            labelText: 'Customer full name',
                            isDense: true,
                            hintText: 'e.g. Juan dela Cruz',
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
                        TextFormField(
                          controller: _phoneController,
                          enabled: !isLoading,
                          decoration: InputDecoration(
                            labelText: 'Phone number',
                            isDense: true,
                            hintText: '09xxxxxxxxx',
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
                          keyboardType: TextInputType.phone,
                          validator:
                              (value) =>
                                  (value == null || value.trim().isEmpty)
                                      ? 'Required'
                                      : null,
                        ),
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _addressController,
                          enabled: !isLoading,
                          decoration: InputDecoration(
                            labelText: 'Address (optional)',
                            isDense: true,
                            hintText: 'Barangay, City',
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
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _creditLimitController,
                          enabled: !isLoading,
                          style: const TextStyle(
                            fontFeatures: [FontFeature.tabularFigures()],
                            fontWeight: FontWeight.w600,
                          ),
                          decoration: InputDecoration(
                            labelText: 'Credit limit',
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
                            if (parsed == null || parsed < 0) {
                              return 'Enter a valid credit limit';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: AppSpacing.md),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: AppSpacing.md,
                            vertical: AppSpacing.xs,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.cardHover,
                            borderRadius: AppRadius.mdBorder,
                            border: Border.all(color: AppColors.border),
                          ),
                          child: ListTile(
                            contentPadding: EdgeInsets.zero,
                            title: Text(
                              _dueDate == null
                                  ? 'No due date set'
                                  : 'Due ${_dueDate!.month}/${_dueDate!.day}/${_dueDate!.year}',
                              style: const TextStyle(
                                fontSize: 14,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                            trailing: TextButton(
                              onPressed: isLoading ? null : _pickDueDate,
                              child: const Text('Pick due date'),
                            ),
                          ),
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
                                      'Add Customer',
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
