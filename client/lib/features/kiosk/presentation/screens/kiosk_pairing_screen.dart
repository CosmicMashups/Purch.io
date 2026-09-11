import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/kiosk_providers.dart';

/// One-time setup screen for a self-order kiosk terminal — pairs by device
/// pairing code alone (no staff PIN, since a kiosk is customer-facing) and
/// then hands off to [onPaired], the same "re-check startup state" callback
/// LoginScreen uses after a staff login.
class KioskPairingScreen extends ConsumerStatefulWidget {
  const KioskPairingScreen({super.key, required this.onPaired});

  final VoidCallback onPaired;

  @override
  ConsumerState<KioskPairingScreen> createState() => _KioskPairingScreenState();
}

class _KioskPairingScreenState extends ConsumerState<KioskPairingScreen> {
  final _formKey = GlobalKey<FormState>();
  final _pairingCodeController = TextEditingController();

  @override
  void dispose() {
    _pairingCodeController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(kioskPairingControllerProvider.notifier);
    await controller.pair(_pairingCodeController.text.trim());

    if (!mounted) {
      return;
    }

    final succeeded = !ref.read(kioskPairingControllerProvider).hasError;
    if (succeeded) {
      widget.onPaired();
    }
  }

  @override
  Widget build(BuildContext context) {
    final pairingState = ref.watch(kioskPairingControllerProvider);
    final isLoading = pairingState.isLoading;
    final failure =
        ref.read(kioskPairingControllerProvider.notifier).currentFailure;

    return Scaffold(
      appBar: AppBar(title: const Text('Kiosk Setup')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: Padding(
              padding: const EdgeInsets.all(32),
              child: Form(
                key: _formKey,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Icon(Icons.point_of_sale, size: 64),
                    const SizedBox(height: 16),
                    const Text(
                      'Pair this device as a self-order kiosk. It will only '
                      'be able to build and submit orders — no payment, '
                      'discounts, or promo codes.',
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 32),
                    TextFormField(
                      controller: _pairingCodeController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Device pairing code',
                        border: OutlineInputBorder(),
                      ),
                      textInputAction: TextInputAction.done,
                      onFieldSubmitted: (_) => _submit(),
                      validator:
                          (value) =>
                              (value == null || value.trim().isEmpty)
                                  ? 'Required'
                                  : null,
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
                                : const Text('Pair This Kiosk'),
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
