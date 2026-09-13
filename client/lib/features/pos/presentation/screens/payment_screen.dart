import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../credit_ledger/presentation/providers/credit_ledger_providers.dart';
import '../../domain/payment_method.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import 'receipt_screen.dart';
import '../../../../core/errors/failure.dart';

/// D5's payment method tabs. Cash, bank transfer, manual GCash QR, and
/// Utang/Credit (Phase 9) have a working checkout flow — the rest are listed
/// but disabled with an explanation.
class PaymentScreen extends ConsumerStatefulWidget {
  const PaymentScreen({super.key, required this.total});

  final double total;

  @override
  ConsumerState<PaymentScreen> createState() => _PaymentScreenState();
}

class _PaymentScreenState extends ConsumerState<PaymentScreen> {
  PaymentMethod? _selectedMethod;
  String? _selectedLedgerId;
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
        customerCreditLedgerId:
            method == PaymentMethod.utangCredit ? _selectedLedgerId : null,
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
            (_change != null && _tendered != null)) &&
        (_selectedMethod != PaymentMethod.utangCredit ||
            _selectedLedgerId != null);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Payment')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 500),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // Total due card
                  Container(
                    decoration: BoxDecoration(
                      color: AppColors.surface,
                      borderRadius: AppRadius.lgBorder,
                      boxShadow: AppShadows.subtle,
                      border: Border.all(color: AppColors.border),
                    ),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 20,
                      vertical: 16,
                    ),
                    child: Column(
                      children: [
                        const Text(
                          'AMOUNT DUE',
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w700,
                            letterSpacing: 1.0,
                            color: AppColors.textSecondary,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          'Total: ₱${widget.total.toStringAsFixed(2)}',
                          style: const TextStyle(
                            fontSize: 26,
                            fontWeight: FontWeight.w800,
                            color: AppColors.brandPrimary,
                            fontFeatures: [FontFeature.tabularFigures()],
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 20),
                  const Text(
                    'Select Payment Method',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 10),

                  // Cash
                  _MethodTile(
                    method: PaymentMethod.cash,
                    label: 'Cash',
                    icon: Icons.payments_rounded,
                    isSelected: _selectedMethod == PaymentMethod.cash,
                    isEnabled: !isLoading,
                    onSelected:
                        () => setState(
                          () => _selectedMethod = PaymentMethod.cash,
                        ),
                  ),
                  if (_selectedMethod == PaymentMethod.cash) ...[
                    const SizedBox(height: 10),
                    Container(
                      padding: const EdgeInsets.all(16),
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: AppRadius.mdBorder,
                        border: Border.all(
                          color: AppColors.brandPrimary.withAlpha(60),
                        ),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
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
                          const SizedBox(height: 10),
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 12,
                              vertical: 8,
                            ),
                            decoration: BoxDecoration(
                              color:
                                  _change != null
                                      ? AppColors.accentEmeraldContainer
                                      : AppColors.cardHover,
                              borderRadius: AppRadius.smBorder,
                            ),
                            child: Text(
                              _change != null
                                  ? 'Change: ₱${_change!.toStringAsFixed(2)}'
                                  : 'Enter an amount of at least the total.',
                              style: TextStyle(
                                fontSize: 14,
                                fontWeight: FontWeight.w600,
                                color:
                                    _change != null
                                        ? AppColors.onAccentEmeraldContainer
                                        : AppColors.textSecondary,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                  const SizedBox(height: 8),

                  // Bank Transfer
                  _MethodTile(
                    method: PaymentMethod.bankTransfer,
                    label: 'Bank Transfer',
                    icon: Icons.account_balance_rounded,
                    isSelected: _selectedMethod == PaymentMethod.bankTransfer,
                    isEnabled: !isLoading,
                    onSelected:
                        () => setState(
                          () => _selectedMethod = PaymentMethod.bankTransfer,
                        ),
                  ),
                  if (_selectedMethod == PaymentMethod.bankTransfer) ...[
                    const SizedBox(height: 8),
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: AppColors.cardHover,
                        borderRadius: AppRadius.smBorder,
                      ),
                      child: const Text(
                        'Confirm only after you\'ve verified the transfer '
                        'landed in your bank account.',
                        style: TextStyle(
                          fontSize: 12,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ),
                  ],
                  const SizedBox(height: 8),

                  // GCash
                  _MethodTile(
                    method: PaymentMethod.manualGcashQr,
                    label: 'GCash (Manual QR)',
                    icon: Icons.qr_code_rounded,
                    isSelected: _selectedMethod == PaymentMethod.manualGcashQr,
                    isEnabled: !isLoading,
                    onSelected:
                        () => setState(
                          () => _selectedMethod = PaymentMethod.manualGcashQr,
                        ),
                  ),
                  if (_selectedMethod == PaymentMethod.manualGcashQr) ...[
                    const SizedBox(height: 8),
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: AppColors.cardHover,
                        borderRadius: AppRadius.smBorder,
                      ),
                      child: const Text(
                        'Show your GCash QR (Business Settings → Branches → '
                        'Manual GCash QR) and confirm only after you\'ve '
                        'verified the payment landed in your account.',
                        style: TextStyle(
                          fontSize: 12,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ),
                  ],
                  const SizedBox(height: 8),

                  // Utang / Credit
                  _MethodTile(
                    method: PaymentMethod.utangCredit,
                    label: 'Utang / Credit',
                    icon: Icons.menu_book_rounded,
                    isSelected: _selectedMethod == PaymentMethod.utangCredit,
                    isEnabled: !isLoading,
                    onSelected:
                        () => setState(
                          () => _selectedMethod = PaymentMethod.utangCredit,
                        ),
                  ),
                  if (_selectedMethod == PaymentMethod.utangCredit) ...[
                    const SizedBox(height: 8),
                    _CreditLedgerPicker(
                      selectedLedgerId: _selectedLedgerId,
                      isEnabled: !isLoading,
                      onChanged: (id) => setState(() => _selectedLedgerId = id),
                    ),
                  ],

                  const SizedBox(height: 20),
                  const Divider(color: AppColors.border, height: 1),
                  const SizedBox(height: 12),
                  const Text(
                    'Other Channels',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      letterSpacing: 0.5,
                      color: AppColors.textMuted,
                    ),
                  ),
                  const SizedBox(height: 8),

                  // Disabled methods
                  _DisabledMethodTile(
                    label: 'QR Ph',
                    icon: Icons.qr_code_2_rounded,
                    reason: 'needs a live Xendit connection',
                  ),
                  const SizedBox(height: 6),
                  _DisabledMethodTile(
                    label: 'Bill Payment / E-Load',
                    icon: Icons.receipt_long_rounded,
                    reason: 'needs the Dragonpay integration',
                  ),
                  const SizedBox(height: 6),
                  _DisabledMethodTile(
                    label: 'Split Payment',
                    icon: Icons.call_split_rounded,
                    reason: 'not built yet',
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
                      child: Text(
                        failure.message,
                        style: const TextStyle(
                          color: AppColors.error,
                          fontSize: 13,
                          fontWeight: FontWeight.w500,
                        ),
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ],
                  const SizedBox(height: 24),
                  SizedBox(
                    height: 52,
                    child: FilledButton(
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColors.brandPrimary,
                        foregroundColor: AppColors.onBrandPrimary,
                        shape: const RoundedRectangleBorder(
                          borderRadius: AppRadius.mdBorder,
                        ),
                      ),
                      onPressed: canConfirm ? _confirm : null,
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
                                'Confirm Payment',
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
    return Container(
      decoration: BoxDecoration(
        color:
            isSelected ? AppColors.brandPrimaryContainer : AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        border: Border.all(
          color: isSelected ? AppColors.brandPrimary : AppColors.border,
          width: isSelected ? 1.5 : 1.0,
        ),
        boxShadow: isSelected ? AppShadows.subtle : null,
      ),
      child: ListTile(
        leading: Icon(
          icon,
          color: isSelected ? AppColors.brandPrimary : AppColors.textSecondary,
        ),
        title: Text(
          label,
          style: TextStyle(
            fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
            color: isSelected ? AppColors.brandPrimary : AppColors.textPrimary,
          ),
        ),
        trailing:
            isSelected
                ? const Icon(
                  Icons.check_circle_rounded,
                  color: AppColors.brandPrimary,
                )
                : null,
        onTap: isEnabled ? onSelected : null,
      ),
    );
  }
}

class _CreditLedgerPicker extends ConsumerWidget {
  const _CreditLedgerPicker({
    required this.selectedLedgerId,
    required this.isEnabled,
    required this.onChanged,
  });

  final String? selectedLedgerId;
  final bool isEnabled;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ledgersAsync = ref.watch(creditLedgerListProvider);

    return ledgersAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error:
          (error, stackTrace) =>
              Text('Could not load customer accounts: ${describeError(error)}'),
      data: (ledgers) {
        if (ledgers.isEmpty) {
          return const Text(
            'No customer accounts yet — add one from Manage Customer '
            'Accounts before charging a sale to utang.',
          );
        }

        return DropdownButtonFormField<String>(
          value: selectedLedgerId,
          decoration: const InputDecoration(
            labelText: 'Customer account',
            border: OutlineInputBorder(),
          ),
          isExpanded: true,
          items: [
            for (final ledger in ledgers)
              DropdownMenuItem(
                value: ledger.id,
                child: Text(
                  '${ledger.customerFullName} (₱${ledger.availableCredit.toStringAsFixed(2)} available)',
                  overflow: TextOverflow.ellipsis,
                ),
              ),
          ],
          onChanged: isEnabled ? onChanged : null,
        );
      },
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
    return Container(
      decoration: BoxDecoration(
        color: AppColors.cardHover.withAlpha(120),
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border.withAlpha(120)),
      ),
      child: ListTile(
        leading: Icon(icon, color: AppColors.textMuted),
        title: Text(
          label,
          style: const TextStyle(
            color: AppColors.textMuted,
            fontWeight: FontWeight.w500,
          ),
        ),
        subtitle: Text(
          'Not yet available — $reason',
          style: const TextStyle(color: AppColors.textMuted, fontSize: 12),
        ),
      ),
    );
  }
}

