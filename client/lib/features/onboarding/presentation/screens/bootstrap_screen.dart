import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/bootstrap_models.dart';
import '../../domain/onboarding_enums.dart';
import '../providers/onboarding_providers.dart';

/// A1–A3 combined into one form, mirroring the backend's single bootstrap
/// endpoint: business type, branding basics come later (Phase 2 continues),
/// but the business identity, first branch, and first admin account all need
/// to exist together before anything else in the app is usable.
class BootstrapScreen extends ConsumerStatefulWidget {
  const BootstrapScreen({super.key});

  @override
  ConsumerState<BootstrapScreen> createState() => _BootstrapScreenState();
}

class _BootstrapScreenState extends ConsumerState<BootstrapScreen> {
  final _formKey = GlobalKey<FormState>();
  final _tenantNameController = TextEditingController();
  final _branchNameController = TextEditingController(text: 'Main Branch');
  final _adminNameController = TextEditingController();
  final _adminPinController = TextEditingController();
  BusinessType _businessType = BusinessType.convenienceStore;

  @override
  void dispose() {
    _tenantNameController.dispose();
    _branchNameController.dispose();
    _adminNameController.dispose();
    _adminPinController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(bootstrapControllerProvider.notifier);
    await controller.bootstrap(
      BootstrapRequest(
        tenantName: _tenantNameController.text.trim(),
        businessType: _businessType,
        branchName: _branchNameController.text.trim(),
        adminName: _adminNameController.text.trim(),
        adminPin: _adminPinController.text.trim(),
      ),
    );

    if (!mounted) {
      return;
    }

    final result = ref.read(bootstrapControllerProvider).valueOrNull;
    if (result != null) {
      await _showPairingCodeDialog(result);
    }
  }

  Future<void> _showPairingCodeDialog(BootstrapResult result) {
    return showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder:
          (dialogContext) => AlertDialog(
            shape: const RoundedRectangleBorder(
              borderRadius: AppRadius.lgBorder,
            ),
            title: const Row(
              children: [
                Icon(
                  Icons.check_circle_outline_rounded,
                  color: AppColors.accentEmerald,
                  size: 24,
                ),
                SizedBox(width: 8),
                Text(
                  'Setup complete',
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 20,
                  ),
                ),
              ],
            ),
            content: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'This device\'s pairing code — write it down, you\'ll need it to log in:',
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 14,
                  ),
                ),
                const SizedBox(height: 16),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 20,
                    vertical: 14,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.brandPrimaryContainer,
                    borderRadius: AppRadius.mdBorder,
                    border: Border.all(
                      color: AppColors.brandPrimary.withAlpha(60),
                    ),
                  ),
                  child: Center(
                    child: SelectableText(
                      result.devicePairingCode,
                      style: const TextStyle(
                        fontSize: 28,
                        fontWeight: FontWeight.w800,
                        letterSpacing: 3.0,
                        color: AppColors.brandPrimary,
                      ),
                    ),
                  ),
                ),
              ],
            ),
            actions: [
              FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.brandPrimary,
                  foregroundColor: AppColors.onBrandPrimary,
                  shape: const RoundedRectangleBorder(
                    borderRadius: AppRadius.mdBorder,
                  ),
                ),
                onPressed: () {
                  Navigator.of(dialogContext).pop();
                  Navigator.of(context).pop(); // back to the login screen
                },
                child: const Text('Continue to Log In'),
              ),
            ],
          ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final bootstrapState = ref.watch(bootstrapControllerProvider);
    final isLoading = bootstrapState.isLoading;
    final failure =
        ref.read(bootstrapControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Set Up Your Business'),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: Container(
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: AppRadius.lgBorder,
                  boxShadow: AppShadows.card,
                  border: Border.all(color: AppColors.border),
                ),
                padding: const EdgeInsets.all(24),
                child: Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      TextFormField(
                        controller: _tenantNameController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Business name',
                          prefixIcon: Icon(Icons.business_rounded, size: 20),
                        ),
                        validator: _requiredValidator,
                      ),
                      const SizedBox(height: 12),
                      DropdownButtonFormField<BusinessType>(
                        value: _businessType,
                        decoration: const InputDecoration(
                          labelText: 'Business type',
                          prefixIcon: Icon(Icons.category_rounded, size: 20),
                        ),
                        items:
                            BusinessType.values
                                .map(
                                  (type) => DropdownMenuItem(
                                    value: type,
                                    child: Text(_label(type)),
                                  ),
                                )
                                .toList(),
                        onChanged:
                            isLoading
                                ? null
                                : (value) => setState(
                                  () => _businessType = value ?? _businessType,
                                ),
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _branchNameController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'First branch name',
                          prefixIcon: Icon(Icons.location_on_rounded, size: 20),
                        ),
                        validator: _requiredValidator,
                      ),
                      const SizedBox(height: 16),
                      Text(
                        'Admin account',
                        style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.w700,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      const SizedBox(height: 8),
                      TextFormField(
                        controller: _adminNameController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Your name',
                          prefixIcon: Icon(Icons.person_rounded, size: 20),
                        ),
                        validator: _requiredValidator,
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _adminPinController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Choose a PIN',
                          prefixIcon: Icon(Icons.lock_rounded, size: 20),
                        ),
                        keyboardType: TextInputType.number,
                        obscureText: true,
                        validator: _requiredValidator,
                      ),
                      if (failure != null) ...[
                        const SizedBox(height: 12),
                        Container(
                          padding: const EdgeInsets.all(10),
                          decoration: BoxDecoration(
                            color: AppColors.error.withAlpha(20),
                            borderRadius: AppRadius.smBorder,
                            border: Border.all(
                              color: AppColors.error.withAlpha(60),
                            ),
                          ),
                          child: Row(
                            children: [
                              const Icon(
                                Icons.error_outline_rounded,
                                size: 16,
                                color: AppColors.error,
                              ),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  failure.message,
                                  style: const TextStyle(
                                    fontSize: 13,
                                    color: AppColors.error,
                                    fontWeight: FontWeight.w500,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                      const SizedBox(height: 18),
                      SizedBox(
                        height: 50,
                        child: FilledButton(
                          onPressed: isLoading ? null : _submit,
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            foregroundColor: AppColors.onBrandPrimary,
                            shape: const RoundedRectangleBorder(
                              borderRadius: AppRadius.mdBorder,
                            ),
                          ),
                          child:
                              isLoading
                                  ? const SizedBox(
                                    height: 20,
                                    width: 20,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2.5,
                                      color: AppColors.onBrandPrimary,
                                    ),
                                  )
                                  : const Text(
                                    'Create Business',
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
    );
  }

  String? _requiredValidator(String? value) =>
      (value == null || value.trim().isEmpty) ? 'Required' : null;

  String _label(BusinessType type) => switch (type) {
    BusinessType.convenienceStore => 'Convenience Store',
    BusinessType.restaurant => 'Restaurant',
    BusinessType.cafe => 'Café',
    BusinessType.clothingShop => 'Clothing Shop',
    BusinessType.departmentStore => 'Department Store',
    BusinessType.groceryStore => 'Grocery Store',
    BusinessType.sariSariStore => 'Sari-Sari Store',
    BusinessType.serviceEstablishment => 'Service Establishment',
    BusinessType.other => 'Other',
  };
}

