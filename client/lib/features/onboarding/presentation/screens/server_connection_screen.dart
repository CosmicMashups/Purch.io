import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/server_connection_providers.dart';
import '../../../../core/theming/app_tokens.dart';

/// Phase 10's "manual-IP-entry" flow: a Local/on-prem installation runs one
/// backend on the store's own LAN, and every physical device (cashier POS,
/// kiosk terminal) needs pointing at that server's address once.
class ServerConnectionScreen extends ConsumerStatefulWidget {
  const ServerConnectionScreen({super.key});

  @override
  ConsumerState<ServerConnectionScreen> createState() =>
      _ServerConnectionScreenState();
}

class _ServerConnectionScreenState
    extends ConsumerState<ServerConnectionScreen> {
  final _formKey = GlobalKey<FormState>();
  final _addressController = TextEditingController();
  bool _savedJustNow = false;

  @override
  void dispose() {
    _addressController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    setState(() => _savedJustNow = false);

    final controller = ref.read(serverConnectionControllerProvider.notifier);
    final succeeded = await controller.testAndSave(
      _addressController.text.trim(),
    );

    if (!mounted) {
      return;
    }

    setState(() => _savedJustNow = succeeded);
  }

  @override
  Widget build(BuildContext context) {
    final connectionState = ref.watch(serverConnectionControllerProvider);
    final isLoading = connectionState.isLoading;
    final failure =
        ref.read(serverConnectionControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Connect to a Local Server'),
      ),
      body: SafeArea(
        child: Center(
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
                padding: const EdgeInsets.all(36),
                child: Form(
                  key: _formKey,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      // Badge icon
                      Center(
                        child: Container(
                          width: 64,
                          height: 64,
                          decoration: BoxDecoration(
                            color: AppColors.brandPrimaryContainer,
                            borderRadius: BorderRadius.circular(AppRadius.lg),
                            border: Border.all(
                              color: AppColors.brandPrimary.withAlpha(40),
                            ),
                          ),
                          child: const Icon(
                            Icons.dns_rounded,
                            size: 32,
                            color: AppColors.brandPrimary,
                          ),
                        ),
                      ),
                      const SizedBox(height: 20),
                      const Text(
                        'Local Server Configuration',
                        style: TextStyle(
                          fontSize: 20,
                          fontWeight: FontWeight.w700,
                          color: AppColors.textPrimary,
                          letterSpacing: -0.2,
                        ),
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        'For a Local/on-prem installation, enter the IP '
                        'address (and port) of the server running on this '
                        'store\'s network — for example 192.168.1.50:5000. '
                        'Ask whoever set up the installer if you\'re not sure.',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontSize: 13,
                          color: AppColors.textSecondary,
                          height: 1.4,
                        ),
                      ),
                      const SizedBox(height: 28),
                      TextFormField(
                        controller: _addressController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Server address',
                          hintText: '192.168.1.50:5000',
                          prefixIcon: Icon(Icons.link_rounded, size: 20),
                        ),
                        keyboardType: TextInputType.url,
                        onFieldSubmitted: (_) => _submit(),
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
                                size: 18,
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
                      if (_savedJustNow) ...[
                        const SizedBox(height: 16),
                        Container(
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(
                            color: AppColors.accentEmeraldContainer,
                            borderRadius: AppRadius.smBorder,
                            border: Border.all(
                              color: AppColors.accentEmerald.withAlpha(60),
                            ),
                          ),
                          child: const Row(
                            children: [
                              Icon(
                                Icons.check_circle_rounded,
                                size: 18,
                                color: AppColors.accentEmerald,
                              ),
                              SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  'Connected — this device will use that server from now on.',
                                  style: TextStyle(
                                    fontSize: 13,
                                    color: AppColors.onAccentEmeraldContainer,
                                    fontWeight: FontWeight.w600,
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
                                    'Test & Save',
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
}

