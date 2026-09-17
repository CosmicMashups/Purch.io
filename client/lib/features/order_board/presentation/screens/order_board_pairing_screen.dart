import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../providers/order_board_providers.dart';

/// One-time setup screen for an Order Number Board terminal (a display,
/// mounted near the counter, showing pending order numbers to waiting
/// customers). Pairing code + PIN, same protected flow as a kiosk.
class OrderBoardPairingScreen extends ConsumerStatefulWidget {
  const OrderBoardPairingScreen({super.key, required this.onPaired});

  final VoidCallback onPaired;

  @override
  ConsumerState<OrderBoardPairingScreen> createState() => _OrderBoardPairingScreenState();
}

class _OrderBoardPairingScreenState extends ConsumerState<OrderBoardPairingScreen> {
  final _formKey = GlobalKey<FormState>();
  final _pairingCodeController = TextEditingController();
  final _pairingPinController = TextEditingController();

  @override
  void dispose() {
    _pairingCodeController.dispose();
    _pairingPinController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final controller = ref.read(orderBoardPairingControllerProvider.notifier);
    await controller.pair(
      _pairingCodeController.text.trim(),
      _pairingPinController.text.trim(),
    );

    if (!mounted) return;

    final succeeded = !ref.read(orderBoardPairingControllerProvider).hasError;
    if (succeeded) {
      widget.onPaired();
    }
  }

  @override
  Widget build(BuildContext context) {
    final pairingState = ref.watch(orderBoardPairingControllerProvider);
    final isLoading = pairingState.isLoading;
    final failure = ref.read(orderBoardPairingControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Order Board Setup'),
        backgroundColor: AppColors.surface,
        centerTitle: true,
        elevation: 0,
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 440),
              child: Padding(
                padding: const EdgeInsets.all(28),
                child: Form(
                  key: _formKey,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Center(
                        child: Container(
                          padding: const EdgeInsets.all(20),
                          decoration: const BoxDecoration(
                            color: AppColors.brandPrimaryContainer,
                            shape: BoxShape.circle,
                          ),
                          child: const Icon(
                            Icons.confirmation_number_rounded,
                            size: 56,
                            color: AppColors.brandPrimary,
                          ),
                        ),
                      ),
                      const SizedBox(height: 24),
                      const Text(
                        'Pair this device as an order number board. It will '
                        'only display pending order numbers, nothing else.',
                        style: TextStyle(
                          fontSize: 15,
                          color: AppColors.textSecondary,
                          height: 1.4,
                        ),
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 32),
                      TextFormField(
                        controller: _pairingCodeController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Device pairing code',
                          border: OutlineInputBorder(borderRadius: AppRadius.mdBorder),
                        ),
                        textInputAction: TextInputAction.next,
                        validator: (value) =>
                            (value == null || value.trim().isEmpty) ? 'Required' : null,
                      ),
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: _pairingPinController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Pairing PIN',
                          helperText: 'Set by an admin in Manage Devices when this board was added.',
                          border: OutlineInputBorder(borderRadius: AppRadius.mdBorder),
                        ),
                        keyboardType: TextInputType.number,
                        obscureText: true,
                        textInputAction: TextInputAction.done,
                        onFieldSubmitted: (_) => _submit(),
                        validator: (value) =>
                            (value == null || value.trim().isEmpty) ? 'Required' : null,
                      ),
                      if (failure != null) ...[
                        const SizedBox(height: 16),
                        Text(
                          failure.message,
                          style: TextStyle(
                            color: Theme.of(context).colorScheme.error,
                            fontWeight: FontWeight.w500,
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ],
                      const SizedBox(height: 28),
                      SizedBox(
                        height: 60,
                        child: FilledButton(
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            foregroundColor: AppColors.onBrandPrimary,
                            shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
                          ),
                          onPressed: isLoading ? null : _submit,
                          child: isLoading
                              ? const SizedBox(
                                  height: 24,
                                  width: 24,
                                  child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white),
                                )
                              : const Text(
                                  'Pair This Board',
                                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
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
