import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/purchase_order_models.dart';
import '../providers/purchase_order_providers.dart';

/// C5's receive-stock-against-PO flow — enter how much of each remaining
/// line arrived in this delivery. A PO can be received across several
/// partial deliveries, so only the remaining (ordered - already received)
/// quantity is offered per line.
class ReceivePurchaseOrderScreen extends ConsumerStatefulWidget {
  const ReceivePurchaseOrderScreen({super.key, required this.purchaseOrder});

  final PurchaseOrder purchaseOrder;

  @override
  ConsumerState<ReceivePurchaseOrderScreen> createState() =>
      _ReceivePurchaseOrderScreenState();
}

class _ReceivePurchaseOrderScreenState
    extends ConsumerState<ReceivePurchaseOrderScreen> {
  late final Map<String, TextEditingController> _controllers;

  @override
  void initState() {
    super.initState();
    _controllers = {
      for (final line in widget.purchaseOrder.lines)
        line.id: TextEditingController(),
    };
  }

  @override
  void dispose() {
    for (final controller in _controllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  bool get _canSubmit {
    var hasAny = false;
    for (final line in widget.purchaseOrder.lines) {
      final text = _controllers[line.id]!.text.trim();
      if (text.isEmpty) {
        continue;
      }
      final quantity = double.tryParse(text);
      if (quantity == null ||
          quantity <= 0 ||
          quantity > line.quantityRemaining) {
        return false;
      }
      hasAny = true;
    }
    return hasAny;
  }

  Future<void> _submit() async {
    if (!_canSubmit) {
      return;
    }

    final lines = <ReceivePurchaseOrderLineRequest>[
      for (final line in widget.purchaseOrder.lines)
        if (_controllers[line.id]!.text.trim().isNotEmpty)
          ReceivePurchaseOrderLineRequest(
            lineId: line.id,
            receivedQuantity: double.parse(_controllers[line.id]!.text.trim()),
          ),
    ];

    final controller = ref.read(
      purchaseOrderActionControllerProvider(widget.purchaseOrder.id).notifier,
    );
    final succeeded = await controller.receive(
      ReceivePurchaseOrderRequest(lines: lines),
    );

    if (!mounted) {
      return;
    }

    if (succeeded) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final actionState = ref.watch(
      purchaseOrderActionControllerProvider(widget.purchaseOrder.id),
    );
    final isLoading = actionState.isLoading;
    final failure =
        ref
            .read(
              purchaseOrderActionControllerProvider(
                widget.purchaseOrder.id,
              ).notifier,
            )
            .currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Receive Stock'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 520),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.xl),
              child: Card(
                elevation: 0,
                color: AppColors.surface,
                shape: const RoundedRectangleBorder(
                  borderRadius: AppRadius.lgBorder,
                  side: BorderSide(color: AppColors.border),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.xl),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        'PO #${widget.purchaseOrder.id.substring(0, widget.purchaseOrder.id.length > 8 ? 8 : widget.purchaseOrder.id.length).toUpperCase()}',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.textMuted,
                          letterSpacing: 0.5,
                        ),
                      ),
                      const SizedBox(height: AppSpacing.xs),
                      Text(
                        widget.purchaseOrder.supplierName,
                        style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: AppColors.textPrimary,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: AppSpacing.lg),
                      for (final line in widget.purchaseOrder.lines)
                        Padding(
                          padding: const EdgeInsets.only(bottom: AppSpacing.md),
                          child: TextField(
                            controller: _controllers[line.id],
                            enabled: !isLoading && line.quantityRemaining > 0,
                            decoration: InputDecoration(
                              labelText: line.itemName,
                              helperText:
                                  line.quantityRemaining > 0
                                      ? '${line.quantityRemaining.toStringAsFixed(0)} remaining of ${line.quantityOrdered.toStringAsFixed(0)} ordered'
                                      : 'Fully received',
                              border: const OutlineInputBorder(
                                borderRadius: AppRadius.smBorder,
                              ),
                            ),
                            keyboardType: const TextInputType.numberWithOptions(
                              decimal: true,
                            ),
                            onChanged: (_) => setState(() {}),
                          ),
                        ),
                      if (failure != null) ...[
                        Container(
                          padding: const EdgeInsets.all(AppSpacing.md),
                          decoration: BoxDecoration(
                            color: AppColors.error.withValues(alpha: 0.08),
                            borderRadius: AppRadius.smBorder,
                            border: Border.all(color: AppColors.error.withValues(alpha: 0.2)),
                          ),
                          child: Text(
                            failure.message,
                            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: AppColors.error,
                            ),
                            textAlign: TextAlign.center,
                          ),
                        ),
                        const SizedBox(height: AppSpacing.md),
                      ],
                      const SizedBox(height: AppSpacing.sm),
                      SizedBox(
                        height: 52,
                        child: FilledButton(
                          onPressed: (!isLoading && _canSubmit) ? _submit : null,
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            shape: const RoundedRectangleBorder(
                              borderRadius: AppRadius.smBorder,
                            ),
                          ),
                          child:
                              isLoading
                                  ? const SizedBox(
                                    height: 20,
                                    width: 20,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2,
                                      color: Colors.white,
                                    ),
                                  )
                                  : const Text('Receive'),
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
