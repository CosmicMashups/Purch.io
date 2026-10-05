import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../onboarding/domain/branch_models.dart';
import '../../../onboarding/presentation/providers/branch_scope_providers.dart';
import '../../domain/incoming_receiving_models.dart';
import '../../domain/purchase_order_models.dart';
import '../../domain/supplier_models.dart';
import '../providers/incoming_receiving_providers.dart';
import '../providers/purchase_order_providers.dart';
import '../providers/supplier_providers.dart';

InputDecoration _decoration(String label) {
  return InputDecoration(
    labelText: label,
    isDense: true,
    filled: true,
    fillColor: AppColors.background,
    border: OutlineInputBorder(
      borderRadius: AppRadius.mdBorder,
      borderSide: const BorderSide(color: AppColors.border),
    ),
  );
}

class _LineDraft {
  _LineDraft({
    String quantity = '',
    String uom = 'pc',
    String unitPrice = '',
  }) : quantityController = TextEditingController(text: quantity),
       uomController = TextEditingController(text: uom),
       unitPriceController = TextEditingController(text: unitPrice);

  Item? item;
  ReceivingCondition condition = ReceivingCondition.good;
  ReceivingRemark remark = ReceivingRemark.accepted;
  final TextEditingController quantityController;
  final TextEditingController uomController;
  final TextEditingController unitPriceController;

  void dispose() {
    quantityController.dispose();
    uomController.dispose();
    unitPriceController.dispose();
  }
}

/// Records an Incoming Receiving Report. Pass [purchaseOrder] to prefill it
/// from an order; leave it out when the order has not been created yet.
class IncomingReceivingFormScreen extends ConsumerStatefulWidget {
  const IncomingReceivingFormScreen({super.key, this.purchaseOrder});

  final PurchaseOrder? purchaseOrder;

  @override
  ConsumerState<IncomingReceivingFormScreen> createState() =>
      _IncomingReceivingFormScreenState();
}

class _IncomingReceivingFormScreenState
    extends ConsumerState<IncomingReceivingFormScreen> {
  Supplier? _supplier;
  Branch? _branch;
  PurchaseOrder? _order;
  DateTime _deliveryDate = DateTime.now();
  final _remarksController = TextEditingController();
  final List<_LineDraft> _lines = [_LineDraft()];

  // Ids until the dropdown data has loaded and the objects can be matched.
  String? _pendingSupplierId;
  String? _pendingBranchId;
  final Map<String, String> _pendingLineItemIds = {};

  @override
  void initState() {
    super.initState();
    final order = widget.purchaseOrder;
    if (order != null) {
      _applyOrder(order);
    }
  }

  @override
  void dispose() {
    _remarksController.dispose();
    for (final line in _lines) {
      line.dispose();
    }
    super.dispose();
  }

  void _applyOrder(PurchaseOrder order) {
    _order = order;
    _pendingSupplierId = order.supplierId;
    _pendingBranchId = order.branchId;
    for (final line in _lines) {
      line.dispose();
    }
    _lines.clear();
    _pendingLineItemIds.clear();
    for (final line in order.lines.where((l) => l.quantityRemaining > 0)) {
      final draft = _LineDraft(
        quantity: line.quantityRemaining.toStringAsFixed(0),
        unitPrice: line.expectedUnitCost.toString(),
      );
      _pendingLineItemIds[draft.hashCode.toString()] = line.itemId;
      _lines.add(draft);
    }
    if (_lines.isEmpty) {
      _lines.add(_LineDraft());
    }
  }

  bool get _canSubmit {
    if (_supplier == null || _branch == null) {
      return false;
    }
    for (final line in _lines) {
      final quantity = double.tryParse(line.quantityController.text.trim());
      final price = double.tryParse(line.unitPriceController.text.trim());
      if (line.item == null ||
          quantity == null ||
          quantity <= 0 ||
          line.uomController.text.trim().isEmpty ||
          price == null ||
          price < 0) {
        return false;
      }
    }
    return true;
  }

  String get _dateText =>
      '${_deliveryDate.year.toString().padLeft(4, '0')}-'
      '${_deliveryDate.month.toString().padLeft(2, '0')}-'
      '${_deliveryDate.day.toString().padLeft(2, '0')}';

  Future<void> _submit() async {
    if (!_canSubmit) {
      return;
    }
    final remarks = _remarksController.text.trim();
    final succeeded = await ref
        .read(incomingReceivingControllerProvider.notifier)
        .create(
          CreateIncomingReceivingRequest(
            purchaseOrderId: _order?.id,
            supplierId: _supplier!.id,
            branchId: _branch!.id,
            deliveryDate: _dateText,
            remarks: remarks.isEmpty ? null : remarks,
            lines: [
              for (final line in _lines)
                CreateIncomingReceivingLineRequest(
                  itemId: line.item!.id,
                  quantityReceived: double.parse(line.quantityController.text.trim()),
                  uom: line.uomController.text.trim(),
                  unitPrice: double.parse(line.unitPriceController.text.trim()),
                  condition: line.condition,
                  remark: line.remark,
                ),
            ],
          ),
        );
    if (mounted && succeeded) {
      Navigator.of(context).pop();
    }
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _deliveryDate,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 1)),
    );
    if (picked != null) {
      setState(() => _deliveryDate = picked);
    }
  }

  @override
  Widget build(BuildContext context) {
    final suppliersAsync = ref.watch(supplierListProvider);
    final branchesAsync = ref.watch(selectableBranchesProvider);
    final itemsAsync = ref.watch(itemListProvider);
    final ordersAsync = ref.watch(purchaseOrderListProvider);
    final state = ref.watch(incomingReceivingControllerProvider);
    final isLoading = state.isLoading;
    final failure =
        ref.read(incomingReceivingControllerProvider.notifier).currentFailure;

    // Resolve prefilled ids once their lists have loaded.
    final suppliers = suppliersAsync.valueOrNull;
    if (_pendingSupplierId != null && suppliers != null) {
      _supplier = suppliers.where((s) => s.id == _pendingSupplierId).firstOrNull;
      _pendingSupplierId = null;
    }
    final branches = branchesAsync.valueOrNull;
    if (_pendingBranchId != null && branches != null) {
      _branch = branches.where((b) => b.id == _pendingBranchId).firstOrNull;
      _pendingBranchId = null;
    }
    final items = itemsAsync.valueOrNull;
    if (_pendingLineItemIds.isNotEmpty && items != null) {
      for (final line in _lines) {
        final itemId = _pendingLineItemIds[line.hashCode.toString()];
        if (itemId != null) {
          line.item = items.where((i) => i.id == itemId).firstOrNull;
        }
      }
      _pendingLineItemIds.clear();
    }

    final openOrders =
        (ordersAsync.valueOrNull ?? const <PurchaseOrder>[])
            .where(
              (o) =>
                  o.status == PurchaseOrderStatus.sent ||
                  o.status == PurchaseOrderStatus.partiallyReceived,
            )
            .toList();

    return Scaffold(
      appBar: AppBar(title: const Text('Record Delivery')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 560),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Container(
                padding: const EdgeInsets.all(AppSpacing.lg),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: AppRadius.lgBorder,
                  border: Border.all(color: AppColors.border),
                  boxShadow: AppShadows.subtle,
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    DropdownButtonFormField<String?>(
                      value: openOrders.any((o) => o.id == _order?.id) ? _order?.id : null,
                      isExpanded: true,
                      decoration: _decoration('Purchase order (optional)'),
                      items: [
                        const DropdownMenuItem<String?>(
                          value: null,
                          child: Text('Not linked to an order'),
                        ),
                        for (final order in openOrders)
                          DropdownMenuItem<String?>(
                            value: order.id,
                            child: Text(
                              '${order.supplierName} → ${order.branchName}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                      ],
                      onChanged: isLoading
                          ? null
                          : (id) => setState(() {
                              if (id == null) {
                                _order = null;
                              } else {
                                _applyOrder(openOrders.firstWhere((o) => o.id == id));
                              }
                            }),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    DropdownButtonFormField<Supplier>(
                      value: suppliers?.where((s) => s.id == _supplier?.id).firstOrNull,
                      isExpanded: true,
                      decoration: _decoration('Supplier'),
                      items: [
                        for (final supplier in suppliers ?? const <Supplier>[])
                          DropdownMenuItem(value: supplier, child: Text(supplier.name, overflow: TextOverflow.ellipsis)),
                      ],
                      onChanged: isLoading ? null : (s) => setState(() => _supplier = s),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    DropdownButtonFormField<Branch>(
                      value: branches?.where((b) => b.id == _branch?.id).firstOrNull,
                      isExpanded: true,
                      decoration: _decoration('Received at branch'),
                      items: [
                        for (final branch in branches ?? const <Branch>[])
                          DropdownMenuItem(value: branch, child: Text(branch.name, overflow: TextOverflow.ellipsis)),
                      ],
                      onChanged: isLoading ? null : (b) => setState(() => _branch = b),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    OutlinedButton.icon(
                      onPressed: isLoading ? null : _pickDate,
                      icon: const Icon(Icons.event),
                      label: Text('Date of delivery: $_dateText'),
                    ),
                    const SizedBox(height: AppSpacing.lg),
                    Text(
                      'Items received',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    for (var i = 0; i < _lines.length; i++)
                      _LineEditor(
                        key: ObjectKey(_lines[i]),
                        items: items ?? const <Item>[],
                        draft: _lines[i],
                        enabled: !isLoading,
                        onChanged: () => setState(() {}),
                        onRemove: _lines.length > 1
                            ? () => setState(() => _lines.removeAt(i).dispose())
                            : null,
                      ),
                    Align(
                      alignment: Alignment.centerLeft,
                      child: TextButton.icon(
                        onPressed: isLoading ? null : () => setState(() => _lines.add(_LineDraft())),
                        icon: const Icon(Icons.add, size: 18),
                        label: const Text('Add Item'),
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    TextField(
                      controller: _remarksController,
                      enabled: !isLoading,
                      maxLines: 2,
                      decoration: _decoration('Notes (optional)'),
                    ),
                    if (failure != null) ...[
                      const SizedBox(height: AppSpacing.md),
                      Container(
                        padding: const EdgeInsets.all(AppSpacing.sm),
                        decoration: BoxDecoration(
                          color: AppColors.cardHover,
                          borderRadius: AppRadius.mdBorder,
                          border: Border.all(color: AppColors.error),
                        ),
                        child: Text(
                          failure.message,
                          style: const TextStyle(color: AppColors.error, fontWeight: FontWeight.w500),
                          textAlign: TextAlign.center,
                        ),
                      ),
                    ],
                    const SizedBox(height: AppSpacing.lg),
                    SizedBox(
                      height: 52,
                      child: FilledButton(
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColors.brandPrimary,
                          foregroundColor: AppColors.onBrandPrimary,
                          shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
                        ),
                        onPressed: (!isLoading && _canSubmit) ? _submit : null,
                        child: isLoading
                            ? const SizedBox(
                                height: 24,
                                width: 24,
                                child: CircularProgressIndicator(strokeWidth: 2.5, color: AppColors.onBrandPrimary),
                              )
                            : const Text('Save Report', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
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

class _LineEditor extends StatelessWidget {
  const _LineEditor({
    super.key,
    required this.items,
    required this.draft,
    required this.enabled,
    required this.onChanged,
    required this.onRemove,
  });

  final List<Item> items;
  final _LineDraft draft;
  final bool enabled;
  final VoidCallback onChanged;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      padding: const EdgeInsets.all(AppSpacing.sm),
      decoration: BoxDecoration(
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          DropdownButtonFormField<Item>(
            value: items.where((i) => i.id == draft.item?.id).firstOrNull,
            isExpanded: true,
            decoration: _decoration('Item'),
            items: [
              for (final item in items)
                DropdownMenuItem(value: item, child: Text(item.name, overflow: TextOverflow.ellipsis)),
            ],
            onChanged: enabled
                ? (item) {
                    draft.item = item;
                    onChanged();
                  }
                : null,
          ),
          const SizedBox(height: AppSpacing.sm),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: draft.quantityController,
                  enabled: enabled,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  decoration: _decoration('Quantity'),
                  onChanged: (_) => onChanged(),
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: TextField(
                  controller: draft.uomController,
                  enabled: enabled,
                  decoration: _decoration('UOM'),
                  onChanged: (_) => onChanged(),
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: TextField(
                  controller: draft.unitPriceController,
                  enabled: enabled,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  decoration: _decoration('Unit price'),
                  onChanged: (_) => onChanged(),
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          Row(
            children: [
              Expanded(
                child: DropdownButtonFormField<ReceivingCondition>(
                  value: draft.condition,
                  decoration: _decoration('Condition'),
                  items: [
                    for (final c in ReceivingCondition.values)
                      DropdownMenuItem(value: c, child: Text(c.label)),
                  ],
                  onChanged: enabled
                      ? (c) {
                          draft.condition = c ?? draft.condition;
                          onChanged();
                        }
                      : null,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: DropdownButtonFormField<ReceivingRemark>(
                  value: draft.remark,
                  decoration: _decoration('Remarks'),
                  items: [
                    for (final r in ReceivingRemark.values)
                      DropdownMenuItem(value: r, child: Text(r.label)),
                  ],
                  onChanged: enabled
                      ? (r) {
                          draft.remark = r ?? draft.remark;
                          onChanged();
                        }
                      : null,
                ),
              ),
            ],
          ),
          if (onRemove != null)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton(
                style: TextButton.styleFrom(foregroundColor: AppColors.error),
                onPressed: enabled ? onRemove : null,
                child: const Text('Remove item'),
              ),
            ),
        ],
      ),
    );
  }
}
