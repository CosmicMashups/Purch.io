import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/payment_method.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import 'receipt_screen.dart';

/// D5's payment method tabs. Only cash, bank transfer, and manual GCash QR
/// have a working checkout flow — the rest are listed but disabled with an
/// explanation, since faking a confirmation for something that touches real
/// money (a gateway webhook, a biller API, a credit ledger) would be worse
/// than just saying it isn't ready yet.
class PaymentScreen extends ConsumerStatefulWidget {
  const PaymentScreen({super.key, required this.total});

  final double total;

  @override
  ConsumerState<PaymentScreen> createState() => _PaymentScreenState();
}

class _PaymentScreenState extends ConsumerState<PaymentScreen> {
  PaymentMethod? _selectedMethod;
  final _tenderedController = TextEditingController();

  @override
  void dispose() {
    _tenderedController.dispose();
    super.dispose();
  }

  double? get _tendered => double.tryParse(_tenderedController.text.trim());

  double? get _change {
    final tendered = _tendered;
    if (tendered == null) {
      return null;
    }
    final change = tendered - widget.total;
    return change < 0 ? null : change;
  }

  Future<void> _confirm() async {
    final method = _selectedMethod;
    if (method == null) {
      return;
    }

    final controller = ref.read(cartNotifierProvider.notifier);
    final succeeded = await controller.recordPayment(
      RecordPaymentRequest(
        method: method,
        amountTendered: method == PaymentMethod.cash ? _tendered : null,
      ),
    );

    if (!mounted) {
      return;
    }

    if (succeeded) {
      await Navigator.of(context).pushReplacement<void, void>(
        MaterialPageRoute(builder: (_) => const ReceiptScreen()),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final cartState = ref.watch(cartNotifierProvider);
    final isLoading = cartState.isLoading;
    final failure = ref.read(cartNotifierProvider.notifier).currentFailure;

    final canConfirm =
        !isLoading &&
        _selectedMethod != null &&
        (_selectedMethod != PaymentMethod.cash ||
            (_change != null && _tendered != null));

    return Scaffold(
      appBar: AppBar(title: const Text('Payment')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Total: ₱${widget.total.toStringAsFixed(2)}',
                    style: Theme.of(context).textTheme.headlineSmall,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 24),
                  _MethodTile(
                    method: PaymentMethod.cash,
                    label: 'Cash',
                    icon: Icons.payments,
                    isSelected: _selectedMethod == PaymentMethod.cash,
                    isEnabled: !isLoading,
                    onSelected:
                        () => setState(
                          () => _selectedMethod = PaymentMethod.cash,
                        ),
                  ),
                  if (_selectedMethod == PaymentMethod.cash) ...[
                    const SizedBox(height: 12),
                    TextField(
                      controller: _tenderedController,
                      enabled: !isLoading,
                      decoration: const InputDecoration(
                        labelText: 'Cash tendered',
                        border: OutlineInputBorder(),
                        prefixText: '₱ ',
                      ),
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      onChanged: (_) => setState(() {}),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      _change != null
                          ? 'Change: ₱${_change!.toStringAsFixed(2)}'
                          : 'Enter an amount of at least the total.',
                    ),
                  ],
                  const SizedBox(height: 8),
                  _MethodTile(
                    method: PaymentMethod.bankTransfer,
                    label: 'Bank Transfer',
                    icon: Icons.account_balance,
                    isSelected: _selectedMethod == PaymentMethod.bankTransfer,
                    isEnabled: !isLoading,
                    onSelected:
                        () => setState(
                          () => _selectedMethod = PaymentMethod.bankTransfer,
                        ),
                  ),
                  if (_selectedMethod == PaymentMethod.bankTransfer) ...[
                    const SizedBox(height: 8),
                    const Text(
                      'Confirm only after you\'ve verified the transfer '
                      'landed in your bank account.',
                    ),
                  ],
                  const SizedBox(height: 8),
                  _MethodTile(
                    method: PaymentMethod.manualGcashQr,
                    label: 'GCash (Manual QR)',
                    icon: Icons.qr_code,
                    isSelected: _selectedMethod == PaymentMethod.manualGcashQr,
                    isEnabled: !isLoading,
                    onSelected:
                        () => setState(
                          () => _selectedMethod = PaymentMethod.manualGcashQr,
                        ),
                  ),
                  if (_selectedMethod == PaymentMethod.manualGcashQr) ...[
                    const SizedBox(height: 8),
                    const Text(
                      'Show your GCash QR (Business Settings → Branches → '
                      'Manual GCash QR) and confirm only after you\'ve '
                      'verified the payment landed in your account.',
                    ),
                  ],
                  const SizedBox(height: 16),
                  const Divider(),
                  const SizedBox(height: 8),
                  _DisabledMethodTile(
                    label: 'QR Ph',
                    icon: Icons.qr_code_2,
                    reason: 'needs a live Xendit connection',
                  ),
                  _DisabledMethodTile(
                    label: 'Bill Payment / E-Load',
                    icon: Icons.receipt_long,
                    reason: 'needs the Dragonpay integration',
                  ),
                  _DisabledMethodTile(
                    label: 'Utang / Credit',
                    icon: Icons.book,
                    reason: 'coming in a later phase',
                  ),
                  _DisabledMethodTile(
                    label: 'Split Payment',
                    icon: Icons.call_split,
                    reason: 'not built yet',
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
                      onPressed: canConfirm ? _confirm : null,
                      child:
                          isLoading
                              ? const SizedBox(
                                height: 24,
                                width: 24,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2.5,
                                ),
                              )
                              : const Text('Confirm Payment'),
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

class _MethodTile extends StatelessWidget {
  const _MethodTile({
    required this.method,
    required this.label,
    required this.icon,
    required this.isSelected,
    required this.isEnabled,
    required this.onSelected,
  });

  final PaymentMethod method;
  final String label;
  final IconData icon;
  final bool isSelected;
  final bool isEnabled;
  final VoidCallback onSelected;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: isSelected ? Theme.of(context).colorScheme.primaryContainer : null,
      child: ListTile(
        leading: Icon(icon),
        title: Text(label),
        trailing: isSelected ? const Icon(Icons.check_circle) : null,
        onTap: isEnabled ? onSelected : null,
      ),
    );
  }
}

class _DisabledMethodTile extends StatelessWidget {
  const _DisabledMethodTile({
    required this.label,
    required this.icon,
    required this.reason,
  });

  final String label;
  final IconData icon;
  final String reason;

  @override
  Widget build(BuildContext context) {
    return Opacity(
      opacity: 0.5,
      child: Card(
        child: ListTile(
          leading: Icon(icon),
          title: Text(label),
          subtitle: Text('Not yet available — $reason'),
        ),
      ),
    );
  }
}
