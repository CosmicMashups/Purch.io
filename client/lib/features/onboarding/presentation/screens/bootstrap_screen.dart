import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
            title: const Text('Setup complete'),
            content: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'This device\'s pairing code — write it down, you\'ll need it to log in:',
                ),
                const SizedBox(height: 12),
                SelectableText(
                  result.devicePairingCode,
                  style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            actions: [
              FilledButton(
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
      appBar: AppBar(title: const Text('Set Up Your Business')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(32),
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
                        border: OutlineInputBorder(),
                      ),
                      validator: _requiredValidator,
                    ),
                    const SizedBox(height: 16),
                    DropdownButtonFormField<BusinessType>(
                      value: _businessType,
                      decoration: const InputDecoration(
                        labelText: 'Business type',
                        border: OutlineInputBorder(),
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
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _branchNameController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'First branch name',
                        border: OutlineInputBorder(),
                      ),
                      validator: _requiredValidator,
                    ),
                    const SizedBox(height: 24),
                    Text(
                      'Admin account',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 8),
                    TextFormField(
                      controller: _adminNameController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Your name',
                        border: OutlineInputBorder(),
                      ),
                      validator: _requiredValidator,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _adminPinController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Choose a PIN',
                        border: OutlineInputBorder(),
                      ),
                      keyboardType: TextInputType.number,
                      obscureText: true,
                      validator: _requiredValidator,
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
                                : const Text('Create Business'),
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
