import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../legal/presentation/widgets/legal_agreement_checkbox.dart';
import '../../domain/bootstrap_models.dart';
import '../../domain/onboarding_enums.dart';
import '../providers/onboarding_providers.dart';
import '../widgets/onboarding_step_progress.dart';

/// Newly-registered tenant setup: business identity, first branch, and first
/// admin account, mirroring the backend's single bootstrap endpoint — but
/// presented as a 3-step wizard so a first-time owner isn't handed one long
/// form. Business identity, first branch, and first admin account all need
/// to exist together before anything else in the app is usable.
class BootstrapScreen extends ConsumerStatefulWidget {
  const BootstrapScreen({super.key});

  @override
  ConsumerState<BootstrapScreen> createState() => _BootstrapScreenState();
}

const _stepLabels = ['Your Business', 'First Branch', 'Admin Account'];

class _BootstrapScreenState extends ConsumerState<BootstrapScreen> {
  final _step1Key = GlobalKey<FormState>();
  final _step2Key = GlobalKey<FormState>();
  final _step3Key = GlobalKey<FormState>();
  final _tenantNameController = TextEditingController();
  final _branchNameController = TextEditingController(text: 'Main Branch');
  final _adminNameController = TextEditingController();
  final _adminPinController = TextEditingController();
  BusinessType _businessType = BusinessType.convenienceStore;
  int _currentStep = 0;
  bool _agreedToLegalTerms = false;
  bool _showAgreementError = false;

  @override
  void dispose() {
    _tenantNameController.dispose();
    _branchNameController.dispose();
    _adminNameController.dispose();
    _adminPinController.dispose();
    super.dispose();
  }

  GlobalKey<FormState> get _currentFormKey => switch (_currentStep) {
    0 => _step1Key,
    1 => _step2Key,
    _ => _step3Key,
  };

  void _goNext() {
    if (!(_currentFormKey.currentState?.validate() ?? false)) {
      return;
    }
    if (_currentStep < _stepLabels.length - 1) {
      setState(() => _currentStep += 1);
      return;
    }
    if (!_agreedToLegalTerms) {
      setState(() => _showAgreementError = true);
      return;
    }
    _submit();
  }

  void _goBack() {
    if (_currentStep > 0) {
      setState(() => _currentStep -= 1);
    } else {
      Navigator.of(context).pop();
    }
  }

  Future<void> _submit() async {
    if (!(_step3Key.currentState?.validate() ?? false)) {
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
    final isLastStep = _currentStep == _stepLabels.length - 1;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Set Up Your Business'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_rounded),
          onPressed: isLoading ? null : _goBack,
        ),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 4,
                      vertical: 8,
                    ),
                    child: OnboardingStepProgress(
                      stepLabels: _stepLabels,
                      currentStep: _currentStep,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Container(
                    decoration: BoxDecoration(
                      color: AppColors.surface,
                      borderRadius: AppRadius.lgBorder,
                      boxShadow: AppShadows.card,
                      border: Border.all(color: AppColors.border),
                    ),
                    padding: const EdgeInsets.all(24),
                    child: AnimatedSwitcher(
                      duration: const Duration(milliseconds: 220),
                      child: KeyedSubtree(
                        key: ValueKey(_currentStep),
                        child: switch (_currentStep) {
                          0 => _BusinessStep(
                            formKey: _step1Key,
                            isLoading: isLoading,
                            nameController: _tenantNameController,
                            businessType: _businessType,
                            onBusinessTypeChanged:
                                (value) => setState(
                                  () =>
                                      _businessType = value ?? _businessType,
                                ),
                          ),
                          1 => _BranchStep(
                            formKey: _step2Key,
                            isLoading: isLoading,
                            branchNameController: _branchNameController,
                          ),
                          _ => _AdminStep(
                            formKey: _step3Key,
                            isLoading: isLoading,
                            adminNameController: _adminNameController,
                            adminPinController: _adminPinController,
                            agreedToLegalTerms: _agreedToLegalTerms,
                            showAgreementError: _showAgreementError,
                            onAgreementChanged:
                                (value) => setState(() {
                                  _agreedToLegalTerms = value;
                                  _showAgreementError = false;
                                }),
                          ),
                        },
                      ),
                    ),
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
                    height: 52,
                    child: FilledButton(
                      onPressed: isLoading ? null : _goNext,
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
                              : Text(
                                isLastStep ? 'Create Business' : 'Continue',
                                style: const TextStyle(
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
    );
  }
}

String? _requiredValidator(String? value) =>
    (value == null || value.trim().isEmpty) ? 'Required' : null;

String _businessTypeLabel(BusinessType type) => switch (type) {
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

class _StepHeader extends StatelessWidget {
  const _StepHeader({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  final IconData icon;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: AppColors.brandPrimaryContainer,
            borderRadius: AppRadius.mdBorder,
          ),
          child: Icon(icon, color: AppColors.brandPrimary, size: 22),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: const TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w800,
                  color: AppColors.textPrimary,
                ),
              ),
              Text(
                subtitle,
                style: const TextStyle(
                  fontSize: 13,
                  color: AppColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _BusinessStep extends StatelessWidget {
  const _BusinessStep({
    required this.formKey,
    required this.isLoading,
    required this.nameController,
    required this.businessType,
    required this.onBusinessTypeChanged,
  });

  final GlobalKey<FormState> formKey;
  final bool isLoading;
  final TextEditingController nameController;
  final BusinessType businessType;
  final ValueChanged<BusinessType?> onBusinessTypeChanged;

  @override
  Widget build(BuildContext context) {
    return Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const _StepHeader(
            icon: Icons.storefront_rounded,
            title: 'Tell us about your business',
            subtitle: 'This becomes your business identity in Purch.io',
          ),
          const SizedBox(height: 20),
          TextFormField(
            controller: nameController,
            enabled: !isLoading,
            decoration: const InputDecoration(
              labelText: 'Business name',
              prefixIcon: Icon(Icons.business_rounded, size: 20),
            ),
            validator: _requiredValidator,
          ),
          const SizedBox(height: 12),
          DropdownButtonFormField<BusinessType>(
            value: businessType,
            decoration: const InputDecoration(
              labelText: 'Business type',
              prefixIcon: Icon(Icons.category_rounded, size: 20),
            ),
            items:
                BusinessType.values
                    .map(
                      (type) => DropdownMenuItem(
                        value: type,
                        child: Text(_businessTypeLabel(type)),
                      ),
                    )
                    .toList(),
            onChanged: isLoading ? null : onBusinessTypeChanged,
          ),
        ],
      ),
    );
  }
}

class _BranchStep extends StatelessWidget {
  const _BranchStep({
    required this.formKey,
    required this.isLoading,
    required this.branchNameController,
  });

  final GlobalKey<FormState> formKey;
  final bool isLoading;
  final TextEditingController branchNameController;

  @override
  Widget build(BuildContext context) {
    return Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const _StepHeader(
            icon: Icons.location_on_rounded,
            title: 'Name your first branch',
            subtitle: 'You can add more branches later from Settings',
          ),
          const SizedBox(height: 20),
          TextFormField(
            controller: branchNameController,
            enabled: !isLoading,
            decoration: const InputDecoration(
              labelText: 'First branch name',
              prefixIcon: Icon(Icons.location_on_rounded, size: 20),
            ),
            validator: _requiredValidator,
          ),
        ],
      ),
    );
  }
}

class _AdminStep extends StatelessWidget {
  const _AdminStep({
    required this.formKey,
    required this.isLoading,
    required this.adminNameController,
    required this.adminPinController,
    required this.agreedToLegalTerms,
    required this.showAgreementError,
    required this.onAgreementChanged,
  });

  final GlobalKey<FormState> formKey;
  final bool isLoading;
  final TextEditingController adminNameController;
  final TextEditingController adminPinController;
  final bool agreedToLegalTerms;
  final bool showAgreementError;
  final ValueChanged<bool> onAgreementChanged;

  @override
  Widget build(BuildContext context) {
    return Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const _StepHeader(
            icon: Icons.admin_panel_settings_rounded,
            title: 'Create your admin account',
            subtitle: 'This PIN logs you in on this and future devices',
          ),
          const SizedBox(height: 20),
          TextFormField(
            controller: adminNameController,
            enabled: !isLoading,
            decoration: const InputDecoration(
              labelText: 'Your name',
              prefixIcon: Icon(Icons.person_rounded, size: 20),
            ),
            validator: _requiredValidator,
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: adminPinController,
            enabled: !isLoading,
            decoration: const InputDecoration(
              labelText: 'Choose a PIN',
              prefixIcon: Icon(Icons.lock_rounded, size: 20),
            ),
            keyboardType: TextInputType.number,
            obscureText: true,
            validator: _requiredValidator,
          ),
          const SizedBox(height: 12),
          LegalAgreementCheckbox(
            value: agreedToLegalTerms,
            enabled: !isLoading,
            onChanged: onAgreementChanged,
            errorText:
                showAgreementError
                    ? 'Please review and accept to continue.'
                    : null,
          ),
        ],
      ),
    );
  }
}
