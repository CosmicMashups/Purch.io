import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../providers/auth_providers.dart';

/// Secondary login path for tenant admins/owners — email+password instead of
/// this device's pairing code + PIN. Reached from [LoginScreen] via a
/// low-emphasis link; day-to-day cashier login is unaffected by this screen.
class AdminLoginScreen extends ConsumerStatefulWidget {
  const AdminLoginScreen({super.key, required this.onLoggedIn});

  final VoidCallback onLoggedIn;

  @override
  ConsumerState<AdminLoginScreen> createState() => _AdminLoginScreenState();
}

class _AdminLoginScreenState extends ConsumerState<AdminLoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(adminLoginControllerProvider.notifier);
    await controller.login(
      email: _emailController.text.trim(),
      password: _passwordController.text,
    );

    if (!mounted) {
      return;
    }

    final succeeded = !ref.read(adminLoginControllerProvider).hasError;
    if (succeeded) {
      widget.onLoggedIn();
    }
  }

  @override
  Widget build(BuildContext context) {
    final loginState = ref.watch(adminLoginControllerProvider);
    final isLoading = loginState.isLoading;
    final failure = ref.read(adminLoginControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Admin Sign In')),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: Container(
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: AppRadius.xlBorder,
                boxShadow: AppShadows.card,
                border: Border.all(color: AppColors.border),
              ),
              padding: const EdgeInsets.all(32),
              child: Form(
                key: _formKey,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Row(
                      children: [
                        Container(
                          width: 44,
                          height: 44,
                          decoration: BoxDecoration(
                            color: AppColors.brandPrimaryContainer,
                            borderRadius: AppRadius.mdBorder,
                          ),
                          child: const Icon(
                            Icons.admin_panel_settings_outlined,
                            color: AppColors.brandPrimary,
                            size: 22,
                          ),
                        ),
                        const SizedBox(width: 12),
                        const Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'Admin Sign In',
                                style: TextStyle(
                                  fontSize: 20,
                                  fontWeight: FontWeight.w800,
                                  color: AppColors.textPrimary,
                                ),
                              ),
                              Text(
                                'For business owners and back-office access',
                                style: TextStyle(
                                  fontSize: 13,
                                  color: AppColors.textSecondary,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 24),

                    TextFormField(
                      controller: _emailController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Email',
                        prefixIcon: Icon(Icons.email_outlined, size: 20),
                      ),
                      keyboardType: TextInputType.emailAddress,
                      textInputAction: TextInputAction.next,
                      validator: (value) =>
                          (value == null || value.trim().isEmpty) ? 'Required' : null,
                    ),
                    const SizedBox(height: 16),

                    TextFormField(
                      controller: _passwordController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Password',
                        prefixIcon: Icon(Icons.lock_outline_rounded, size: 20),
                      ),
                      obscureText: true,
                      textInputAction: TextInputAction.done,
                      onFieldSubmitted: (_) => _submit(),
                      validator: (value) =>
                          (value == null || value.isEmpty) ? 'Required' : null,
                    ),

                    if (failure != null) ...[
                      const SizedBox(height: 16),
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: AppColors.error.withAlpha(20),
                          borderRadius: AppRadius.smBorder,
                          border: Border.all(color: AppColors.error.withAlpha(60)),
                        ),
                        child: Row(
                          children: [
                            const Icon(Icons.error_outline_rounded, size: 18, color: AppColors.error),
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

                    const SizedBox(height: 24),

                    SizedBox(
                      height: 52,
                      child: FilledButton(
                        onPressed: isLoading ? null : _submit,
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColors.brandPrimary,
                          foregroundColor: AppColors.onBrandPrimary,
                          shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
                        ),
                        child: isLoading
                            ? const SizedBox(
                                height: 20,
                                width: 20,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2.5,
                                  color: AppColors.onBrandPrimary,
                                ),
                              )
                            : const Text(
                                'Sign In',
                                style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
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
    );
  }
}
