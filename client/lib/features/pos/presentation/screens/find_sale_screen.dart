import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/formatting/money.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import '../widgets/refund_dialog.dart';

/// Looking up an older sale by its receipt number — for a refund the cashier
/// can't reach from an open cart or the receipt they just printed (an
/// earlier day, a different terminal). Receipt numbers are only unique per
/// device, so a search can rarely come back with more than one sale; the
/// cashier picks from the short list in that case instead of guessing.
class FindSaleScreen extends ConsumerStatefulWidget {
  const FindSaleScreen({super.key});

  @override
  ConsumerState<FindSaleScreen> createState() => _FindSaleScreenState();
}

class _FindSaleScreenState extends ConsumerState<FindSaleScreen> {
  final _controller = TextEditingController();
  bool _busy = false;
  String? _error;
  List<Transaction>? _matches;
  Transaction? _selected;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _search() async {
    final receiptNumber = int.tryParse(_controller.text.trim());
    if (receiptNumber == null || receiptNumber <= 0) {
      setState(() {
        _error = 'Enter the receipt number as it appears on the printed receipt.';
        _matches = null;
        _selected = null;
      });
      return;
    }

    setState(() {
      _busy = true;
      _error = null;
      _matches = null;
      _selected = null;
    });

    try {
      final found = await ref
          .read(posRepositoryProvider)
          .findByReceiptNumber(receiptNumber);
      if (!mounted) return;
      setState(() {
        if (found.isEmpty) {
          _error = 'No completed sale found with receipt number $receiptNumber.';
        } else if (found.length == 1) {
          _selected = found.first;
        } else {
          _matches = found;
        }
      });
    } on Failure catch (failure) {
      if (!mounted) return;
      setState(() => _error = failure.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _refund(Transaction sale) async {
    final repository = ref.read(posRepositoryProvider);
    final refunded = await showRefundDialog(
      context: context,
      total: sale.totalAmount,
      submit: (reason, approverPin) async {
        try {
          final result = await repository.refundTransaction(
            sale.id,
            RefundTransactionRequest(reason: reason, approverPin: approverPin),
          );
          if (mounted) setState(() => _selected = result);
          return null;
        } on Failure catch (failure) {
          return failure;
        }
      },
    );
    if (refunded && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Sale refunded.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final selected = _selected;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Find a Sale')),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'Look up a completed sale by its receipt number to refund it.',
                style: TextStyle(color: AppColors.textSecondary),
              ),
              const SizedBox(height: AppSpacing.md),
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _controller,
                      enabled: !_busy,
                      autofocus: true,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Receipt number',
                        hintText: 'e.g. 1047',
                      ),
                      onSubmitted: (_) => _search(),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.md),
                  FilledButton(
                    onPressed: _busy ? null : _search,
                    child: _busy
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text('Find'),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.lg),
              if (_error != null)
                EmptyStateView(
                  icon: Icons.search_off_outlined,
                  title: 'Sale not found',
                  description: _error!,
                ),
              if (_matches != null)
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${_matches!.length} sales share that receipt number (different terminals number independently). Pick the one you mean.',
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                      const SizedBox(height: AppSpacing.sm),
                      Expanded(
                        child: ListView.separated(
                          itemCount: _matches!.length,
                          separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
                          itemBuilder: (context, index) {
                            final sale = _matches![index];
                            return _SaleCard(
                              sale: sale,
                              onTap: () => setState(() => _selected = sale),
                            );
                          },
                        ),
                      ),
                    ],
                  ),
                ),
              if (selected != null) _SaleSummary(sale: selected, onRefund: () => _refund(selected)),
            ],
          ),
        ),
      ),
    );
  }
}

class _SaleCard extends StatelessWidget {
  const _SaleCard({required this.sale, required this.onTap});

  final Transaction sale;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surface,
      borderRadius: AppRadius.mdBorder,
      child: InkWell(
        borderRadius: AppRadius.mdBorder,
        onTap: onTap,
        child: Container(
          decoration: BoxDecoration(
            borderRadius: AppRadius.mdBorder,
            border: Border.all(color: AppColors.border),
          ),
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  sale.lines.isEmpty
                      ? 'Sale'
                      : '${sale.lines.first.itemName}${sale.lines.length > 1 ? ' + ${sale.lines.length - 1} more' : ''}',
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
              ),
              Text(
                formatCurrency(sale.totalAmount),
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _SaleSummary extends StatelessWidget {
  const _SaleSummary({required this.sale, required this.onRefund});

  final Transaction sale;
  final VoidCallback onRefund;

  @override
  Widget build(BuildContext context) {
    final refunded = sale.status == TransactionStatus.refunded;

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Receipt No. ${sale.receiptNumber ?? '—'}',
            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
          ),
          if (refunded) ...[
            const SizedBox(height: 4),
            const Text(
              'REFUNDED',
              style: TextStyle(color: AppColors.error, fontWeight: FontWeight.w800),
            ),
          ],
          const SizedBox(height: AppSpacing.sm),
          for (final line in sale.lines)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Expanded(child: Text('${line.itemName} x${line.quantity.toStringAsFixed(0)}')),
                  Text(formatCurrency(line.lineTotal)),
                ],
              ),
            ),
          const SizedBox(height: AppSpacing.sm),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text('Total', style: TextStyle(fontWeight: FontWeight.w800)),
              Text(
                formatCurrency(sale.totalAmount),
                style: const TextStyle(fontWeight: FontWeight.w800, color: AppColors.brandPrimary),
              ),
            ],
          ),
          if (!refunded) ...[
            const SizedBox(height: AppSpacing.md),
            SizedBox(
              height: 44,
              child: OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColors.error,
                  side: const BorderSide(color: AppColors.error),
                ),
                onPressed: onRefund,
                icon: const Icon(Icons.undo_rounded, size: 18),
                label: const Text('Refund'),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
