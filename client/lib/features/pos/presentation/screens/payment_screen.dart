import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/formatting/money.dart';
import '../../../../core/hardware/hardware_providers.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../credit_ledger/presentation/providers/credit_ledger_providers.dart';
import '../../../onboarding/presentation/providers/onboarding_providers.dart';
import '../../domain/payment_method.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import 'receipt_screen.dart';

/// D5's payment drawer overlay. Cash, bank transfer, manual GCash QR, and
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
  bool _printReceipt = true;

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

  void _onKeypadTap(String key) {
    var text = _tenderedController.text;
    if (key == 'backspace') {
      if (text.isNotEmpty) {
        _tenderedController.text = text.substring(0, text.length - 1);
      }
    } else if (key == '.') {
      if (!text.contains('.')) {
        _tenderedController.text = text.isEmpty ? '0.' : '$text.';
      }
    } else {
      if (text == '0') {
        _tenderedController.text = key;
      } else {
        _tenderedController.text = '$text$key';
      }
    }
    setState(() {});
  }

  void _setExact() {
    setState(() {
      _tenderedController.text = widget.total.toStringAsFixed(2);
    });
  }

  void _roundUp() {
    final total = widget.total;
    final nextHundred = ((total / 100).ceil()) * 100.0;
    final rounded = nextHundred <= total ? total + 100.0 : nextHundred;
    setState(() {
      _tenderedController.text = rounded.toStringAsFixed(2);
    });
  }

  void _setDenomination(double amount) {
    setState(() {
      _tenderedController.text = amount.toStringAsFixed(2);
    });
  }

  void _addQuick(double delta) {
    final current = _tendered ?? 0.0;
    setState(() {
      _tenderedController.text = (current + delta).toStringAsFixed(2);
    });
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
      // 1. Kick cash drawer if cash sale
      if (method == PaymentMethod.cash) {
        await ref.read(cashDrawerServiceProvider).kickOnCashSale(
              operatorName: 'Cashier',
            );
      }

      // 2. Direct ESC/POS thermal printing if requested
      final completedCart = ref.read(cartNotifierProvider).valueOrNull;
      if (completedCart != null) {
        ref.read(cfdServiceProvider).updateFromTransaction(transaction: completedCart);

        if (_printReceipt) {
          final tenantSettings = ref.read(tenantSettingsNotifierProvider).valueOrNull;
          await ref.read(printerServiceProvider).printReceipt(
                transaction: completedCart,
                tenantSettings: tenantSettings,
                cashierName: 'Cashier',
                cutPaper: true,
              );
        }
      }

      if (!mounted) return;
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
      appBar: AppBar(
        title: const Text('Payment'),
        actions: [
          IconButton(
            icon: const Icon(Icons.close_rounded),
            tooltip: 'Cancel (Esc)',
            onPressed: () => Navigator.of(context).maybePop(),
          ),
        ],
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 680),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // --- TOTAL DUE & TICKET SUMMARY CARD ---
                  _buildSummaryCard(),

                  const SizedBox(height: 18),

                  // --- SELECT PAYMENT METHOD TABS ---
                  const Text(
                    'Select Payment Method',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 10),
                  _buildMethodTabs(isLoading),

                  const SizedBox(height: 16),

                  // --- METHOD DETAILS CONTENT ---
                  if (_selectedMethod == PaymentMethod.cash)
                    _buildCashTenderView(isLoading)
                  else if (_selectedMethod == PaymentMethod.bankTransfer)
                    _buildBankTransferView()
                  else if (_selectedMethod == PaymentMethod.manualGcashQr)
                    _buildGcashView()
                  else if (_selectedMethod == PaymentMethod.utangCredit)
                    _buildCreditLedgerView(isLoading),

                  const SizedBox(height: 18),
                  const Divider(color: AppColors.border, height: 1),
                  const SizedBox(height: 14),

                  // --- OTHER CHANNELS ---
                  _buildOtherChannels(),

                  if (failure != null) ...[
                    const SizedBox(height: 16),
                    Container(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      decoration: BoxDecoration(
                        color: AppColors.errorContainer,
                        borderRadius: BorderRadius.circular(AppRadius.sm),
                        border: Border.all(color: AppColors.errorBorder),
                      ),
                      child: Text(
                        failure.message,
                        style: const TextStyle(
                          color: AppColors.onErrorContainer,
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                        ),
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ],

                  const SizedBox(height: 16),

                  // --- PRINT BIR RECEIPT TOGGLE ---
                  InkWell(
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                    onTap: () => setState(() => _printReceipt = !_printReceipt),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: 4),
                      child: Row(
                        children: [
                          Checkbox(
                            value: _printReceipt,
                            activeColor: AppColors.brandPrimary,
                            onChanged: (v) => setState(() => _printReceipt = v ?? true),
                          ),
                          const Text(
                            'Print BIR Official Receipt',
                            style: TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w600,
                              color: AppColors.textPrimary,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),

                  const SizedBox(height: 16),

                  // --- CONFIRM BUTTON ---
                  SizedBox(
                    height: 52,
                    child: FilledButton(
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColors.brandPrimary,
                        foregroundColor: Colors.white,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(AppRadius.md),
                        ),
                        elevation: 2,
                      ),
                      onPressed: canConfirm ? _confirm : null,
                      child: isLoading
                          ? const SizedBox(
                              height: 22,
                              width: 22,
                              child: CircularProgressIndicator(
                                strokeWidth: 2.5,
                                color: Colors.white,
                              ),
                            )
                          : FittedBox(
                              fit: BoxFit.scaleDown,
                              child: Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  const Icon(Icons.point_of_sale_rounded, size: 20),
                                  const SizedBox(width: 8),
                                  const Text(
                                    'Confirm Payment',
                                    style: TextStyle(
                                      fontSize: 16,
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                  const SizedBox(width: 12),
                                  Container(
                                    width: 1,
                                    height: 20,
                                    color: Colors.white.withValues(alpha: 0.3),
                                  ),
                                  const SizedBox(width: 12),
                                  Text(
                                    formatCurrency(widget.total),
                                    style: const TextStyle(
                                      fontSize: 16,
                                      fontWeight: FontWeight.w800,
                                      fontFeatures: [FontFeature.tabularFigures()],
                                    ),
                                  ),
                                ],
                              ),
                            ),
                    ),
                  ),
                  const SizedBox(height: 12),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildSummaryCard() {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        boxShadow: AppShadows.subtle,
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: AppColors.brandPrimary,
              borderRadius: BorderRadius.circular(AppRadius.md),
            ),
            child: const Icon(
              Icons.payments_rounded,
              color: Colors.white,
              size: 24,
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  crossAxisAlignment: WrapCrossAlignment.center,
                  spacing: 8,
                  runSpacing: 4,
                  children: [
                    const Text(
                      'Tender Payment',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w800,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                      decoration: BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        borderRadius: BorderRadius.circular(AppRadius.full),
                        border: Border.all(
                          color: AppColors.brandPrimary.withValues(alpha: 0.3),
                        ),
                      ),
                      child: const Text(
                        'Ticket #0042',
                        style: TextStyle(
                          fontSize: 10,
                          fontWeight: FontWeight.w700,
                          color: AppColors.onBrandPrimaryContainer,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 2),
                const Text(
                  'Counter Sale • Main Branch',
                  style: TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              const Text(
                'AMOUNT DUE',
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.w800,
                  letterSpacing: 0.8,
                  color: AppColors.textSecondary,
                ),
              ),
              Text(
                'Total: ${formatCurrency(widget.total)}',
                style: const TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w900,
                  color: AppColors.brandPrimary,
                  fontFeatures: [FontFeature.tabularFigures()],
                  letterSpacing: -0.4,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildMethodTabs(bool isLoading) {
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        _MethodTabButton(
          label: 'Cash',
          badge: 'F1',
          icon: Icons.payments_rounded,
          isSelected: _selectedMethod == PaymentMethod.cash,
          onTap: isLoading ? null : () => setState(() => _selectedMethod = PaymentMethod.cash),
        ),
        _MethodTabButton(
          label: 'Bank Transfer',
          badge: 'Bank',
          icon: Icons.account_balance_rounded,
          isSelected: _selectedMethod == PaymentMethod.bankTransfer,
          onTap: isLoading ? null : () => setState(() => _selectedMethod = PaymentMethod.bankTransfer),
        ),
        _MethodTabButton(
          label: 'GCash (Manual QR)',
          badge: 'Manual QR',
          icon: Icons.qr_code_2_rounded,
          isSelected: _selectedMethod == PaymentMethod.manualGcashQr,
          onTap: isLoading ? null : () => setState(() => _selectedMethod = PaymentMethod.manualGcashQr),
        ),
        _MethodTabButton(
          label: 'Utang / Credit',
          badge: 'Ledger',
          icon: Icons.menu_book_rounded,
          isSelected: _selectedMethod == PaymentMethod.utangCredit,
          onTap: isLoading ? null : () => setState(() => _selectedMethod = PaymentMethod.utangCredit),
        ),
      ],
    );
  }

  Widget _buildCashTenderView(bool isLoading) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          padding: const EdgeInsets.all(AppSpacing.md),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadius.md),
            border: Border.all(color: AppColors.border),
            boxShadow: AppShadows.subtle,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text(
                    'Payable Total',
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  Text(
                    formatCurrency(widget.total),
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w800,
                      color: AppColors.textPrimary,
                      fontFeatures: [FontFeature.tabularFigures()],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              TextField(
                controller: _tenderedController,
                enabled: !isLoading,
                decoration: InputDecoration(
                  labelText: 'Cash tendered',
                  prefixText: '₱ ',
                  prefixStyle: const TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.w800,
                    color: AppColors.brandPrimary,
                  ),
                  filled: true,
                  fillColor: AppColors.background,
                  contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadius.md),
                    borderSide: const BorderSide(color: AppColors.brandPrimary, width: 1.5),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadius.md),
                    borderSide: const BorderSide(color: AppColors.brandPrimary, width: 1.5),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadius.md),
                    borderSide: const BorderSide(color: AppColors.brandPrimary, width: 2),
                  ),
                  suffixIcon: IconButton(
                    icon: const Icon(Icons.backspace_outlined, size: 20),
                    color: AppColors.textSecondary,
                    tooltip: 'Clear',
                    onPressed: () {
                      _tenderedController.clear();
                      setState(() {});
                    },
                  ),
                ),
                style: const TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.w800,
                  color: AppColors.textPrimary,
                  fontFeatures: [FontFeature.tabularFigures()],
                ),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                onChanged: (_) => setState(() {}),
              ),
              const SizedBox(height: 10),

              // Change Due Banner
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                decoration: BoxDecoration(
                  color: _change != null ? const Color(0xFFDCFCE7) : const Color(0xFFF1F5F9),
                  borderRadius: BorderRadius.circular(AppRadius.md),
                  border: Border.all(
                    color: _change != null ? const Color(0xFF86EFAC) : AppColors.border,
                  ),
                ),
                child: Row(
                  children: [
                    Icon(
                      Icons.change_circle_rounded,
                      size: 24,
                      color: _change != null ? const Color(0xFF166534) : AppColors.textSecondary,
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            _change != null
                                ? 'Change: ${formatCurrency(_change!)}'
                                : 'Enter an amount of at least the total.',
                            style: TextStyle(
                              fontSize: 14,
                              fontWeight: FontWeight.w700,
                              color: _change != null ? const Color(0xFF166534) : AppColors.textSecondary,
                            ),
                          ),
                          if (_change != null)
                            const Text(
                              'Return exact bills & coins to customer',
                              style: TextStyle(
                                fontSize: 11,
                                color: Color(0xFF15803D),
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                        ],
                      ),
                    ),
                    if (_change != null)
                      Text(
                        formatCurrency(_change!),
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w900,
                          color: Color(0xFF166534),
                          fontFeatures: [FontFeature.tabularFigures()],
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),

        const SizedBox(height: 14),

        // Quick Bills & Fast Denominations
        const Text(
          'QUICK BILLS & FAST DENOMINATIONS',
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w800,
            letterSpacing: 0.6,
            color: AppColors.textSecondary,
          ),
        ),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            _DenominationChip(
              label: 'Exact',
              amount: formatCurrency(widget.total),
              isPrimary: true,
              onTap: _setExact,
            ),
            _DenominationChip(
              label: 'Round Up',
              amount: formatCurrency(((widget.total / 100).ceil()) * 100),
              isPrimaryContainer: true,
              onTap: _roundUp,
            ),
            _DenominationChip(
              label: 'Bill',
              amount: '₱1,000.00',
              onTap: () => _setDenomination(1000),
            ),
            _DenominationChip(
              label: 'Bill',
              amount: '₱500.00',
              onTap: () => _setDenomination(500),
            ),
            _DenominationChip(
              label: 'Bill',
              amount: '₱200.00',
              onTap: () => _setDenomination(200),
            ),
            _DenominationChip(
              label: 'Add',
              amount: '+₱20.00',
              onTap: () => _addQuick(20),
            ),
          ],
        ),

        const SizedBox(height: 14),

        // Touch Numeric Keypad
        Container(
          padding: const EdgeInsets.all(AppSpacing.md),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadius.lg),
            border: Border.all(color: AppColors.border),
            boxShadow: AppShadows.subtle,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text(
                    'TOUCH NUMERIC KEYPAD',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 0.6,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                    decoration: BoxDecoration(
                      color: AppColors.brandPrimaryContainer,
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: const Text(
                      'ACTIVE',
                      style: TextStyle(
                        fontSize: 9,
                        fontWeight: FontWeight.w800,
                        color: AppColors.brandPrimary,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Column(
                children: [
                  _buildKeypadRow(['1', '2', '3']),
                  const SizedBox(height: 8),
                  _buildKeypadRow(['4', '5', '6']),
                  const SizedBox(height: 8),
                  _buildKeypadRow(['7', '8', '9']),
                  const SizedBox(height: 8),
                  _buildKeypadRow(['.', '0', 'backspace']),
                ],
              ),
              const SizedBox(height: 10),
              const Wrap(
                alignment: WrapAlignment.spaceBetween,
                spacing: 8,
                runSpacing: 4,
                children: [
                  Text(
                    'Hardware Drawer: Connected',
                    style: TextStyle(fontSize: 11, color: AppColors.textSecondary),
                  ),
                  Text(
                    'Auto-kick on tender',
                    style: TextStyle(fontSize: 11, color: AppColors.textSecondary),
                  ),
                ],
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildKeypadRow(List<String> keys) {
    return Row(
      children: [
        for (final key in keys) ...[
          Expanded(
            child: _KeypadButton(
              keyLabel: key,
              onTap: () => _onKeypadTap(key),
            ),
          ),
          if (key != keys.last) const SizedBox(width: 8),
        ],
      ],
    );
  }

  Widget _buildBankTransferView() {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(Icons.account_balance_rounded, color: AppColors.brandPrimary),
              SizedBox(width: 8),
              Text(
                'Direct Bank Transfer',
                style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: AppColors.background,
              borderRadius: BorderRadius.circular(AppRadius.sm),
            ),
            child: const Text(
              'Confirm only after you\'ve verified the transfer landed in your bank account.',
              style: TextStyle(fontSize: 12, color: AppColors.textSecondary),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildGcashView() {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(Icons.qr_code_2_rounded, color: AppColors.brandPrimary),
              SizedBox(width: 8),
              Text(
                'GCash (Manual QR)',
                style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: AppColors.background,
              borderRadius: BorderRadius.circular(AppRadius.sm),
            ),
            child: const Text(
              'Show your GCash QR (Business Settings → Branches → Manual GCash QR) '
              'and confirm only after you\'ve verified the payment landed in your account.',
              style: TextStyle(fontSize: 12, color: AppColors.textSecondary),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCreditLedgerView(bool isLoading) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(Icons.menu_book_rounded, color: AppColors.brandPrimary),
              SizedBox(width: 8),
              Text(
                'Utang / Credit',
                style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
              ),
            ],
          ),
          const SizedBox(height: 10),
          _CreditLedgerPicker(
            selectedLedgerId: _selectedLedgerId,
            isEnabled: !isLoading,
            onChanged: (id) => setState(() => _selectedLedgerId = id),
          ),
        ],
      ),
    );
  }

  Widget _buildOtherChannels() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'OTHER CHANNELS',
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w800,
            letterSpacing: 0.6,
            color: AppColors.textMuted,
          ),
        ),
        const SizedBox(height: 8),
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
      ],
    );
  }
}

class _MethodTabButton extends StatelessWidget {
  const _MethodTabButton({
    required this.label,
    required this.badge,
    required this.icon,
    required this.isSelected,
    required this.onTap,
  });

  final String label;
  final String badge;
  final IconData icon;
  final bool isSelected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: isSelected ? AppColors.brandPrimary : AppColors.surface,
      borderRadius: BorderRadius.circular(AppRadius.md),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadius.md),
            border: Border.all(
              color: isSelected ? AppColors.brandPrimary : AppColors.border,
              width: isSelected ? 1.5 : 1.0,
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                icon,
                size: 18,
                color: isSelected ? Colors.white : AppColors.textSecondary,
              ),
              const SizedBox(width: 8),
              Text(
                label,
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: isSelected ? FontWeight.w800 : FontWeight.w600,
                  color: isSelected ? Colors.white : AppColors.textPrimary,
                ),
              ),
              const SizedBox(width: 6),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1.5),
                decoration: BoxDecoration(
                  color: isSelected
                      ? Colors.white.withValues(alpha: 0.25)
                      : AppColors.background,
                  borderRadius: BorderRadius.circular(4),
                ),
                child: Text(
                  badge,
                  style: TextStyle(
                    fontSize: 9,
                    fontWeight: FontWeight.w700,
                    color: isSelected ? Colors.white : AppColors.textSecondary,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _DenominationChip extends StatelessWidget {
  const _DenominationChip({
    required this.label,
    required this.amount,
    required this.onTap,
    this.isPrimary = false,
    this.isPrimaryContainer = false,
  });

  final String label;
  final String amount;
  final VoidCallback onTap;
  final bool isPrimary;
  final bool isPrimaryContainer;

  @override
  Widget build(BuildContext context) {
    Color bg = AppColors.surface;
    Color textCol = AppColors.textPrimary;
    Color borderCol = AppColors.border;

    if (isPrimary) {
      bg = AppColors.brandPrimary;
      textCol = Colors.white;
      borderCol = AppColors.brandPrimary;
    } else if (isPrimaryContainer) {
      bg = AppColors.brandPrimaryContainer;
      textCol = AppColors.onBrandPrimaryContainer;
      borderCol = AppColors.brandPrimary.withValues(alpha: 0.3);
    }

    return Material(
      color: bg,
      borderRadius: BorderRadius.circular(AppRadius.md),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: onTap,
        child: Container(
          constraints: const BoxConstraints(minWidth: 88, minHeight: 46),
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadius.md),
            border: Border.all(color: borderCol),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(
                label.toUpperCase(),
                style: TextStyle(
                  fontSize: 9,
                  fontWeight: FontWeight.w700,
                  letterSpacing: 0.5,
                  color: isPrimary ? Colors.white.withValues(alpha: 0.8) : AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                amount,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w800,
                  fontFeatures: const [FontFeature.tabularFigures()],
                  color: textCol,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _KeypadButton extends StatelessWidget {
  const _KeypadButton({required this.keyLabel, required this.onTap});

  final String keyLabel;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final isBackspace = keyLabel == 'backspace';

    return Material(
      color: isBackspace ? AppColors.errorContainer : AppColors.background,
      borderRadius: BorderRadius.circular(AppRadius.md),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: onTap,
        child: Container(
          height: 48,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadius.md),
            border: Border.all(
              color: isBackspace ? AppColors.errorBorder : AppColors.border,
            ),
          ),
          child: isBackspace
              ? const Icon(
                  Icons.backspace_outlined,
                  size: 20,
                  color: AppColors.onErrorContainer,
                )
              : Text(
                  keyLabel,
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                    color: AppColors.textPrimary,
                  ),
                ),
        ),
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
                  '${ledger.customerFullName} (${formatCurrency(ledger.availableCredit)} available)',
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
        color: AppColors.background,
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: AppColors.border),
      ),
      child: ListTile(
        leading: Icon(icon, color: AppColors.textMuted),
        title: Text(
          label,
          style: const TextStyle(
            color: AppColors.textMuted,
            fontWeight: FontWeight.w600,
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
