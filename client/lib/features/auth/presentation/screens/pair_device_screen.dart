import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/theming/app_tokens.dart';
import '../providers/auth_providers.dart';

/// Pairs this device with the one-time code an admin generated on the Devices
/// page. The code works once and expires in minutes; the device then keeps its
/// own credential, so nothing here is ever a person's password or PIN.
class PairDeviceScreen extends ConsumerStatefulWidget {
  const PairDeviceScreen({super.key, required this.onPaired});

  /// Called once the device is paired, so the app can reset and route by device type.
  final VoidCallback onPaired;

  @override
  ConsumerState<PairDeviceScreen> createState() => _PairDeviceScreenState();
}

class _PairDeviceScreenState extends ConsumerState<PairDeviceScreen> {
  final _formKey = GlobalKey<FormState>();
  final _codeController = TextEditingController();

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final session = await ref
        .read(pairControllerProvider.notifier)
        .pair(_codeController.text.trim());

    if (!mounted || session == null) {
      return;
    }
    widget.onPaired();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(pairControllerProvider);
    final isLoading = state.isLoading;
    final failure = ref.read(pairControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Pair this device'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_rounded),
          onPressed: () => context.go('/login'),
        ),
      ),
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
                    const Text(
                      'Enter the pairing code',
                      style: TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.w800,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 6),
                    const Text(
                      'An admin makes this code on the Devices page. It works once and expires in a few minutes.',
                      style: TextStyle(
                        fontSize: 13,
                        color: AppColors.textSecondary,
                      ),
                    ),
                    const SizedBox(height: 24),
                    TextFormField(
                      controller: _codeController,
                      enabled: !isLoading,
                      autofocus: true,
                      textCapitalization: TextCapitalization.characters,
                      decoration: const InputDecoration(
                        labelText: 'Pairing code',
                        prefixIcon: Icon(Icons.qr_code_2_rounded, size: 20),
                      ),
                      textInputAction: TextInputAction.done,
                      onFieldSubmitted: (_) => isLoading ? null : _submit(),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
                    ),
                    if (failure != null) ...[
                      const SizedBox(height: 16),
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: AppColors.errorContainer,
                          borderRadius: AppRadius.smBorder,
                        ),
                        child: Text(
                          failure.message,
                          style: const TextStyle(
                            fontSize: 13,
                            color: AppColors.error,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                    const SizedBox(height: 24),
                    FilledButton(
                      onPressed: isLoading ? null : _submit,
                      child:
                          isLoading
                              ? const SizedBox(
                                width: 20,
                                height: 20,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                              : const Text('Pair device'),
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
