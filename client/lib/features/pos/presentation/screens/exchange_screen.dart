import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/formatting/money.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/domain/item_variant_models.dart';
import '../../../catalog/domain/pricing_type.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../domain/exchange_models.dart';
import '../../domain/payment_method.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';

/// The only ways an exchange difference can be settled today (the server rejects the rest).
const _settlementMethods = [
  (PaymentMethod.cash, 'Cash'),
  (PaymentMethod.bankTransfer, 'Bank transfer'),
  (PaymentMethod.manualGcashQr, 'GCash'),
];

class _Replacement {
  _Replacement({
    required this.itemId,
    required this.itemVariantId,
    required this.name,
    required this.detail,
    required this.unitPrice,
  });

  final String itemId;
  final String? itemVariantId;
  final String name;
  final String? detail;
  final double unitPrice;
  double quantity = 1;

  String get key => '$itemId:${itemVariantId ?? ''}';
}

/// Exchange against a completed sale: some of its lines come back at the price they sold for, other
/// items go out at today's price, and the difference is settled. The totals shown here are only a
/// preview for the cashier; the server prices and validates everything and always needs a manager or
/// admin PIN.
class ExchangeScreen extends ConsumerStatefulWidget {
  const ExchangeScreen({required this.sale, super.key});

  final Transaction sale;

  @override
  ConsumerState<ExchangeScreen> createState() => _ExchangeScreenState();
}

class _ExchangeScreenState extends ConsumerState<ExchangeScreen> {
  final _search = TextEditingController();
  final _tendered = TextEditingController();
  final _reason = TextEditingController();
  final _pin = TextEditingController();

  final Map<String, double> _returns = {};
  final List<_Replacement> _replacements = [];
  PaymentMethod _settlement = PaymentMethod.cash;
  Item? _pickingVariantFor;
  List<ItemVariant>? _variants;
  bool _busy = false;
  String? _error;
  Adjustment? _done;

  /// What earlier exchanges left of each line. Until it loads (or if the lookup fails) a line can go up
  /// to what was bought; the server still checks, so a slow lookup never blocks the cashier.
  Map<String, double> _returnable = const {};

  @override
  void initState() {
    super.initState();
    _loadReturnable();
  }

  Future<void> _loadReturnable() async {
    try {
      final returnable = await ref
          .read(posRepositoryProvider)
          .listReturnableLines(widget.sale.id);
      if (mounted) setState(() => _returnable = returnable);
    } on Failure {
      // Keep the optimistic caps.
    }
  }

  @override
  void dispose() {
    _search.dispose();
    _tendered.dispose();
    _reason.dispose();
    _pin.dispose();
    super.dispose();
  }

  double get _returnedTotal => widget.sale.lines.fold(
    0.0,
    (sum, line) => sum + (_returns[line.id] ?? 0) * line.unitPrice,
  );

  double get _replacementTotal =>
      _replacements.fold(0.0, (sum, r) => sum + r.unitPrice * r.quantity);

  double get _difference => _replacementTotal - _returnedTotal;
  bool get _hasReturn => _returns.values.any((q) => q > 0);
  bool get _owed => _difference > 0;
  double get _tenderedAmount => double.tryParse(_tendered.text.trim()) ?? 0;
  bool get _cashShort =>
      _owed &&
      _settlement == PaymentMethod.cash &&
      _tenderedAmount < _difference;

  bool get _ready =>
      _hasReturn &&
      _replacements.isNotEmpty &&
      _reason.text.trim().isNotEmpty &&
      _pin.text.trim().isNotEmpty &&
      !_cashShort &&
      !_busy;

  List<Item> _matches(List<Item> items) {
    final term = _search.text.trim().toLowerCase();
    if (term.isEmpty) return const [];
    return items
        .where(
          (i) =>
              i.isActive &&
              (i.pricingType == PricingType.unit ||
                  i.pricingType == PricingType.variantMatrix) &&
              (i.name.toLowerCase().contains(term) ||
                  i.sku?.toLowerCase() == term ||
                  i.barcode == term),
        )
        .take(6)
        .toList();
  }

  String _returnSubtitle(TransactionLine line) {
    final bought =
        'Bought ${_fmt(line.quantity)} at ${formatCurrency(line.unitPrice)}';
    final remaining = _returnable[line.id];
    if (remaining == null || remaining >= line.quantity) return bought;
    return remaining > 0
        ? '$bought, ${_fmt(remaining)} left to return'
        : '$bought, all already returned';
  }

  void _setReturn(String lineId, double quantity) =>
      setState(() => _returns[lineId] = quantity);

  void _add(Item item, ItemVariant? variant) {
    final replacement = _Replacement(
      itemId: item.id,
      itemVariantId: variant?.id,
      name: item.name,
      detail: variant?.attributesLabel,
      unitPrice: variant?.priceOverride ?? item.basePrice,
    );
    setState(() {
      final existing = _replacements.where((r) => r.key == replacement.key);
      if (existing.isNotEmpty) {
        existing.first.quantity += 1;
      } else {
        _replacements.add(replacement);
      }
      _search.clear();
      _pickingVariantFor = null;
      _variants = null;
    });
  }

  Future<void> _choose(Item item) async {
    if (item.pricingType != PricingType.variantMatrix) {
      _add(item, null);
      return;
    }
    setState(() {
      _pickingVariantFor = item;
      _variants = null;
    });
    try {
      final variants = await ref
          .read(catalogRepositoryProvider)
          .listVariants(item.id);
      if (mounted && _pickingVariantFor?.id == item.id) {
        setState(() => _variants = variants);
      }
    } on Failure catch (failure) {
      if (mounted) setState(() => _error = failure.message);
    }
  }

  Future<void> _submit() async {
    if (!_ready) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    final needsSettlement = _difference != 0;
    try {
      final result = await ref
          .read(posRepositoryProvider)
          .createExchange(
            widget.sale.id,
            ExchangeRequest(
              returnLines: [
                for (final line in widget.sale.lines)
                  if ((_returns[line.id] ?? 0) > 0)
                    ExchangeReturnLine(
                      originalLineId: line.id,
                      quantity: _returns[line.id]!,
                    ),
              ],
              replacementLines: [
                for (final r in _replacements)
                  ExchangeReplacementLine(
                    itemId: r.itemId,
                    itemVariantId: r.itemVariantId,
                    quantity: r.quantity,
                  ),
              ],
              reason: _reason.text.trim(),
              approverPin: _pin.text.trim(),
              settlementMethod: needsSettlement ? _settlement : null,
              settlementAmountTendered:
                  _owed && _settlement == PaymentMethod.cash
                      ? _tenderedAmount
                      : null,
            ),
          );
      if (mounted) setState(() => _done = result);
    } on Failure catch (failure) {
      // The server's own words (wrong PIN, nothing left to return, an item that can't be exchanged
      // yet) are the useful ones, so they stay on the screen with every field still filled in.
      if (mounted) setState(() => _error = failure.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final done = _done;
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Exchange receipt No. ${widget.sale.receiptNumber ?? '—'}'),
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child:
                done != null
                    ? _Done(adjustment: done)
                    : _form(context),
          ),
        ),
      ),
    );
  }

  Widget _form(BuildContext context) {
    final items = ref.watch(itemListProvider).valueOrNull ?? const <Item>[];
    final matches = _matches(items);
    final ready = _hasReturn && _replacements.isNotEmpty;
    final title =
        !ready
            ? 'Difference'
            : _difference == 0
            ? 'Even exchange'
            : _owed
            ? 'Customer pays'
            : 'Refund to customer';

    return ListView(
      padding: const EdgeInsets.all(AppSpacing.lg),
      children: [
        const _Heading('Coming back'),
        for (final line in widget.sale.lines)
          _StepperRow(
            title: line.itemName,
            subtitle: _returnSubtitle(line),
            value: _returns[line.id] ?? 0,
            max: _returnable[line.id] ?? line.quantity,
            onChanged: (q) => _setReturn(line.id, q),
          ),
        const SizedBox(height: AppSpacing.xl),
        const _Heading('Taking instead'),
        TextField(
          key: const Key('exchange-search'),
          controller: _search,
          decoration: const InputDecoration(
            labelText: 'Find an item',
            helperText:
                'Plain items and variants only. Combos and weighed items can\'t be exchanged yet.',
            helperMaxLines: 2,
          ),
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: AppSpacing.sm),
        if (_pickingVariantFor != null)
          _VariantChooser(
            item: _pickingVariantFor!,
            variants: _variants,
            onChoose: (v) => _add(_pickingVariantFor!, v),
            onCancel:
                () => setState(() {
                  _pickingVariantFor = null;
                  _variants = null;
                }),
          )
        else if (_search.text.trim().isNotEmpty) ...[
          if (matches.isEmpty)
            Text(
              'No exchangeable item matches "${_search.text.trim()}".',
              style: const TextStyle(color: AppColors.textSecondary),
            ),
          for (final item in matches)
            Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.sm),
              child: OutlinedButton(
                onPressed: () => _choose(item),
                child: Row(
                  children: [
                    Expanded(child: Text(item.name)),
                    Text(
                      item.pricingType == PricingType.variantMatrix
                          ? 'Choose a variant'
                          : formatCurrency(item.basePrice),
                    ),
                  ],
                ),
              ),
            ),
        ],
        if (_replacements.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: AppSpacing.md),
            child: Text(
              'Nothing added yet. Search above to add what the customer is taking.',
              style: TextStyle(color: AppColors.textSecondary),
            ),
          ),
        for (final r in _replacements)
          _StepperRow(
            title: r.name,
            subtitle:
                '${r.detail != null ? '${r.detail}, ' : ''}${formatCurrency(r.unitPrice)} each',
            value: r.quantity,
            onChanged:
                (q) => setState(() {
                  if (q <= 0) {
                    _replacements.remove(r);
                  } else {
                    r.quantity = q;
                  }
                }),
          ),
        const SizedBox(height: AppSpacing.xl),
        Container(
          padding: const EdgeInsets.all(AppSpacing.lg),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: AppRadius.mdBorder,
            border: Border.all(color: AppColors.border),
          ),
          child: Column(
            children: [
              _SummaryRow('Coming back', formatCurrency(_returnedTotal)),
              _SummaryRow('Taking instead', formatCurrency(_replacementTotal)),
              const Divider(color: AppColors.border),
              _SummaryRow(
                title,
                formatCurrency(_difference.abs()),
                key: const Key('exchange-difference'),
                strong: true,
              ),
              const SizedBox(height: AppSpacing.xs),
              const Text(
                'A preview. The server prices the exchange and confirms the final amounts.',
                style: TextStyle(
                  fontSize: 12,
                  color: AppColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
        if (ready && _difference != 0) ...[
          const SizedBox(height: AppSpacing.xl),
          _Heading(
            _owed ? 'How is the customer paying?' : 'How is the refund given?',
          ),
          Wrap(
            spacing: AppSpacing.sm,
            children: [
              for (final (method, label) in _settlementMethods)
                ChoiceChip(
                  label: Text(label),
                  selected: _settlement == method,
                  onSelected: (_) => setState(() => _settlement = method),
                ),
            ],
          ),
          if (_owed && _settlement == PaymentMethod.cash) ...[
            const SizedBox(height: AppSpacing.md),
            TextField(
              key: const Key('exchange-tendered'),
              controller: _tendered,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(
                labelText: 'Cash received',
                errorText:
                    _tendered.text.isNotEmpty && _cashShort
                        ? 'Needs at least ${formatCurrency(_difference)}.'
                        : null,
              ),
              onChanged: (_) => setState(() {}),
            ),
            if (!_cashShort && _tendered.text.isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: AppSpacing.sm),
                child: Text(
                  'Change: ${formatCurrency(_tenderedAmount - _difference)}',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
              ),
          ],
        ],
        const SizedBox(height: AppSpacing.xl),
        const _Heading('Approval'),
        TextField(
          key: const Key('exchange-reason'),
          controller: _reason,
          minLines: 2,
          maxLines: 3,
          decoration: const InputDecoration(labelText: 'Reason'),
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: AppSpacing.md),
        TextField(
          key: const Key('exchange-pin'),
          controller: _pin,
          obscureText: true,
          keyboardType: TextInputType.number,
          decoration: const InputDecoration(
            labelText: 'Manager or admin PIN',
            prefixIcon: Icon(Icons.shield_outlined, size: 20),
          ),
          onChanged: (_) => setState(() {}),
        ),
        if (_error != null) ...[
          const SizedBox(height: AppSpacing.md),
          Text(
            _error!,
            style: const TextStyle(
              color: AppColors.error,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
        const SizedBox(height: AppSpacing.lg),
        SizedBox(
          height: 52,
          child: FilledButton(
            key: const Key('record-exchange'),
            onPressed: _ready ? _submit : null,
            child: Text(_busy ? 'Recording...' : 'Record exchange'),
          ),
        ),
      ],
    );
  }
}

String _fmt(double quantity) =>
    quantity.toStringAsFixed(quantity.truncateToDouble() == quantity ? 0 : 2);

class _Heading extends StatelessWidget {
  const _Heading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: AppSpacing.sm),
    child: Text(
      text,
      style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
    ),
  );
}

class _StepperRow extends StatelessWidget {
  const _StepperRow({
    required this.title,
    required this.subtitle,
    required this.value,
    required this.onChanged,
    this.max,
  });

  final String title;
  final String subtitle;
  final double value;
  final double? max;
  final ValueChanged<double> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.md,
        vertical: AppSpacing.sm,
      ),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
                Text(
                  subtitle,
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          IconButton(
            tooltip: 'Decrease $title',
            onPressed: value <= 0 ? null : () => onChanged(value - 1),
            icon: const Icon(Icons.remove_circle_outline),
          ),
          SizedBox(
            width: 28,
            child: Text(
              _fmt(value),
              textAlign: TextAlign.center,
              style: const TextStyle(fontWeight: FontWeight.w800),
            ),
          ),
          IconButton(
            tooltip: 'Increase $title',
            onPressed:
                max != null && value >= max! ? null : () => onChanged(value + 1),
            icon: const Icon(Icons.add_circle_outline),
          ),
        ],
      ),
    );
  }
}

class _SummaryRow extends StatelessWidget {
  const _SummaryRow(this.label, this.value, {super.key, this.strong = false});

  final String label;
  final String value;
  final bool strong;

  @override
  Widget build(BuildContext context) {
    final style = TextStyle(
      fontSize: strong ? 18 : 14,
      fontWeight: strong ? FontWeight.w800 : FontWeight.w500,
    );
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [Text(label, style: style), Text(value, style: style)],
      ),
    );
  }
}

class _VariantChooser extends StatelessWidget {
  const _VariantChooser({
    required this.item,
    required this.variants,
    required this.onChoose,
    required this.onCancel,
  });

  final Item item;
  final List<ItemVariant>? variants;
  final ValueChanged<ItemVariant> onChoose;
  final VoidCallback onCancel;

  @override
  Widget build(BuildContext context) {
    final list = variants;
    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Which ${item.name}?',
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: AppSpacing.sm),
          if (list == null)
            const Text('Loading variants...')
          else if (list.isEmpty)
            const Text('This item has no variants set up.')
          else
            Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.sm,
              children: [
                for (final variant in list)
                  OutlinedButton(
                    onPressed: () => onChoose(variant),
                    child: Text(
                      '${variant.attributes.values.join(', ')}  ${formatCurrency(variant.priceOverride ?? item.basePrice)}',
                    ),
                  ),
              ],
            ),
          TextButton(onPressed: onCancel, child: const Text('Cancel')),
        ],
      ),
    );
  }
}

class _Done extends StatelessWidget {
  const _Done({required this.adjustment});

  final Adjustment adjustment;

  @override
  Widget build(BuildContext context) {
    final difference = adjustment.priceDifference;
    final headline =
        difference == 0
            ? 'Even exchange recorded'
            : difference > 0
            ? 'Collected ${formatCurrency(difference)}'
            : 'Refund ${formatCurrency(-difference)} to the customer';
    return ListView(
      padding: const EdgeInsets.all(AppSpacing.lg),
      children: [
        const Text(
          'Exchange recorded',
          style: TextStyle(
            fontSize: 24,
            fontWeight: FontWeight.w800,
            color: AppColors.brandPrimary,
          ),
        ),
        const SizedBox(height: AppSpacing.xs),
        Text(
          'Against receipt No. ${adjustment.originalReceiptNumber ?? '—'}, approved by ${adjustment.approvedByName}.',
          style: const TextStyle(color: AppColors.textSecondary),
        ),
        const SizedBox(height: AppSpacing.lg),
        _Lines('Came back', adjustment.returnLines),
        const SizedBox(height: AppSpacing.md),
        _Lines('Taken instead', adjustment.replacementLines),
        const SizedBox(height: AppSpacing.lg),
        Text(
          headline,
          style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
        ),
        if (adjustment.changeGiven != null && adjustment.changeGiven! > 0)
          Padding(
            padding: const EdgeInsets.only(top: AppSpacing.xs),
            child: Text(
              'Change: ${formatCurrency(adjustment.changeGiven!)}',
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
          ),
        const SizedBox(height: AppSpacing.xl),
        SizedBox(
          height: 52,
          child: FilledButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Done'),
          ),
        ),
      ],
    );
  }
}

class _Lines extends StatelessWidget {
  const _Lines(this.title, this.lines);

  final String title;
  final List<AdjustmentLine> lines;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
        for (final line in lines)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 2),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('${line.itemName} x${_fmt(line.quantity)}'),
                Text(formatCurrency(line.lineTotal)),
              ],
            ),
          ),
      ],
    );
  }
}
