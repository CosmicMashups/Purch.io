import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_combo_component_models.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';

/// D2 — High-end tactile combo and meal customization modal dialog/screen.
/// Faithfully aligns with `purch.io_pos_pork_sisig_combo_modal_d2` mockup:
/// squircle image badge, COMBO tag, step-by-step required option cards,
/// quantity stepper, and live calculating Add to Cart button.
class ComboCustomizationScreen extends ConsumerStatefulWidget {
  const ComboCustomizationScreen({super.key, required this.item, this.addLine});

  final Item item;

  /// Overrides how an add-to-cart is performed — defaults to the POS cart
  /// (cartNotifierProvider) when omitted. The kiosk feature passes its own
  /// kioskCartNotifierProvider-backed callback to reuse this screen as-is.
  final Future<bool> Function(AddTransactionLineRequest)? addLine;

  @override
  ConsumerState<ComboCustomizationScreen> createState() =>
      _ComboCustomizationScreenState();
}

class _ComboCustomizationScreenState
    extends ConsumerState<ComboCustomizationScreen> {
  final Map<String, List<String>> _selectedItemIdsBySlot = {};
  int _quantity = 1;

  void _toggleSelection(ItemComboComponent slot, String itemId) {
    setState(() {
      final selected = _selectedItemIdsBySlot.putIfAbsent(slot.id, () => []);
      if (selected.contains(itemId)) {
        selected.remove(itemId);
        return;
      }
      if (selected.length >= slot.quantity) {
        selected.removeAt(0);
      }
      selected.add(itemId);
    });
  }

  bool _isComplete(List<ItemComboComponent> slots) {
    return slots.every(
      (slot) => (_selectedItemIdsBySlot[slot.id]?.length ?? 0) == slot.quantity,
    );
  }

  double _calculateTotal(List<ItemComboComponent> slots) {
    double total = widget.item.basePrice * _quantity;
    for (final slot in slots) {
      if (slot.substitutionUpchargeAmount != null &&
          slot.substitutionUpchargeAmount! > 0) {
        final count = _selectedItemIdsBySlot[slot.id]?.length ?? 0;
        total += slot.substitutionUpchargeAmount! * count * _quantity;
      }
    }
    return total;
  }

  Future<void> _addToCart(List<ItemComboComponent> slots) async {
    final selections = <ComboSelectionRequest>[
      for (final slot in slots)
        for (final itemId in _selectedItemIdsBySlot[slot.id] ?? const <String>[])
          ComboSelectionRequest(slotId: slot.id, selectedItemId: itemId),
    ];

    final add =
        widget.addLine ??
        (request) => ref.read(cartNotifierProvider.notifier).addLine(request);
    final succeeded = await add(
      AddTransactionLineRequest(
        itemId: widget.item.id,
        quantity: _quantity.toDouble(),
        comboSelections: selections,
      ),
    );
    if (succeeded && mounted) {
      Navigator.of(context).pop();
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text('Added ${widget.item.name}')));
    }
  }

  Widget _buildItemHeaderCard() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.subtle,
      ),
      child: Row(
        children: [
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: AppColors.brandPrimaryContainer,
              borderRadius: BorderRadius.circular(AppRadius.md),
              border: Border.all(
                color: AppColors.brandPrimary.withValues(alpha: 0.2),
              ),
            ),
            child: widget.item.imageUrl != null
                ? ClipRRect(
                    borderRadius: BorderRadius.circular(AppRadius.md),
                    child: Image.network(
                      widget.item.imageUrl!,
                      fit: BoxFit.cover,
                      errorBuilder: (_, __, ___) => const Icon(
                        Icons.restaurant_menu_rounded,
                        color: AppColors.brandPrimary,
                        size: 28,
                      ),
                    ),
                  )
                : const Icon(
                    Icons.restaurant_menu_rounded,
                    color: AppColors.brandPrimary,
                    size: 28,
                  ),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  crossAxisAlignment: WrapCrossAlignment.center,
                  spacing: 8,
                  runSpacing: 4,
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 2,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.accentWarmContainer,
                        borderRadius: BorderRadius.circular(AppRadius.full),
                      ),
                      child: const Text(
                        'COMBO',
                        style: TextStyle(
                          fontSize: 10,
                          fontWeight: FontWeight.w800,
                          color: AppColors.onAccentWarmContainer,
                          letterSpacing: 0.5,
                        ),
                      ),
                    ),
                    const Text(
                      'Configurable Combo • Dine-In & Takeout',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                Text(
                  widget.item.name,
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '₱${widget.item.basePrice.toStringAsFixed(2)}',
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                    color: AppColors.brandPrimary,
                    fontFeatures: [FontFeature.tabularFigures()],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBottomBar(List<ItemComboComponent> slots) {
    final complete = _isComplete(slots);
    final total = _calculateTotal(slots);

    return Container(
      decoration: const BoxDecoration(
        color: AppColors.surface,
        boxShadow: AppShadows.card,
        border: Border(top: BorderSide(color: AppColors.border)),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
      child: SafeArea(
        top: false,
        child: Row(
          children: [
            // Quantity Stepper
            Container(
              decoration: BoxDecoration(
                color: AppColors.background,
                borderRadius: BorderRadius.circular(AppRadius.md),
                border: Border.all(color: AppColors.border),
              ),
              padding: const EdgeInsets.all(2),
              child: Row(
                children: [
                  IconButton(
                    icon: const Icon(Icons.remove_rounded, size: 18),
                    visualDensity: VisualDensity.compact,
                    color: _quantity > 1
                        ? AppColors.textPrimary
                        : AppColors.textMuted,
                    onPressed: _quantity > 1
                        ? () => setState(() => _quantity--)
                        : null,
                  ),
                  SizedBox(
                    width: 32,
                    child: Text(
                      '$_quantity',
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w800,
                        fontFeatures: [FontFeature.tabularFigures()],
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                  IconButton(
                    icon: const Icon(Icons.add_rounded, size: 18),
                    visualDensity: VisualDensity.compact,
                    color: AppColors.textPrimary,
                    onPressed: () => setState(() => _quantity++),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            // Cancel button
            TextButton(
              onPressed: () => Navigator.of(context).maybePop(),
              child: const Text(
                'Cancel',
                style: TextStyle(
                  color: AppColors.textSecondary,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            const SizedBox(width: 12),
            // Primary Confirm Add to Cart Action
            Expanded(
              child: SizedBox(
                height: 50,
                child: FilledButton(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.brandPrimary,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppRadius.md),
                    ),
                    elevation: 2,
                  ),
                  onPressed: complete ? () => _addToCart(slots) : null,
                  child: FittedBox(
                    fit: BoxFit.scaleDown,
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.check_circle_rounded, size: 20),
                        const SizedBox(width: 8),
                        const Text(
                          'Add to Cart',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Container(
                          width: 1,
                          height: 18,
                          color: Colors.white.withValues(alpha: 0.3),
                        ),
                        const SizedBox(width: 12),
                        Text(
                          '₱${total.toStringAsFixed(2)}',
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
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final slotsAsync = ref.watch(
      itemComboComponentListProvider(widget.item.id),
    );
    final itemsAsync = ref.watch(itemListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(widget.item.name),
        actions: [
          IconButton(
            icon: const Icon(Icons.close_rounded),
            tooltip: 'Close',
            onPressed: () => Navigator.of(context).maybePop(),
          ),
        ],
      ),
      body: slotsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) => Center(
              child: Text(
                'Could not load this combo: ${describeError(error)}',
                style: const TextStyle(color: AppColors.error),
              ),
            ),
        data: (slots) {
          if (slots.isEmpty) {
            return Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 64,
                    height: 64,
                    decoration: BoxDecoration(
                      color: AppColors.brandPrimaryContainer,
                      borderRadius: BorderRadius.circular(AppRadius.lg),
                    ),
                    child: const Icon(
                      Icons.set_meal_outlined,
                      size: 32,
                      color: AppColors.brandPrimary,
                    ),
                  ),
                  const SizedBox(height: 16),
                  const Text(
                    'No slots have been configured for this combo yet.',
                    style: TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            );
          }

          return itemsAsync.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error:
                (error, stackTrace) => Center(
                  child: Text(
                    'Could not load items: ${describeError(error)}',
                    style: const TextStyle(color: AppColors.error),
                  ),
                ),
            data: (items) {
              return Column(
                children: [
                  Expanded(
                    child: Center(
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 640),
                        child: ListView(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 20,
                            vertical: 16,
                          ),
                          children: [
                            _buildItemHeaderCard(),
                            const SizedBox(height: 16),
                            for (int i = 0; i < slots.length; i++) ...[
                              _SlotSection(
                                index: i,
                                slot: slots[i],
                                candidateItems:
                                    items
                                        .where(
                                          (item) =>
                                              item.categoryId ==
                                                  slots[i].componentCategoryId &&
                                              item.isActive,
                                        )
                                        .toList(),
                                selectedItemIds:
                                    _selectedItemIdsBySlot[slots[i].id] ??
                                    const [],
                                onToggle:
                                    (itemId) =>
                                        _toggleSelection(slots[i], itemId),
                              ),
                              if (i != slots.length - 1)
                                const SizedBox(height: 14),
                            ],
                          ],
                        ),
                      ),
                    ),
                  ),
                  _buildBottomBar(slots),
                ],
              );
            },
          );
        },
      ),
    );
  }
}

class _SlotSection extends StatelessWidget {
  const _SlotSection({
    required this.index,
    required this.slot,
    required this.candidateItems,
    required this.selectedItemIds,
    required this.onToggle,
  });

  final int index;
  final ItemComboComponent slot;
  final List<Item> candidateItems;
  final List<String> selectedItemIds;
  final ValueChanged<String> onToggle;

  @override
  Widget build(BuildContext context) {
    final isSatisfied = selectedItemIds.length == slot.quantity;

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        boxShadow: AppShadows.subtle,
        border: Border.all(
          color: isSatisfied
              ? AppColors.brandPrimary.withValues(alpha: 0.3)
              : AppColors.border,
        ),
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Text(
                  'STEP ${index + 1}: ${slot.slotLabel.toUpperCase()} (CHOOSE ${slot.quantity})',
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 0.6,
                    color: AppColors.textPrimary,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: isSatisfied
                      ? const Color(0xFFDCFCE7)
                      : AppColors.brandPrimaryContainer,
                  borderRadius: BorderRadius.circular(4),
                  border: Border.all(
                    color: isSatisfied
                        ? const Color(0xFF86EFAC)
                        : AppColors.brandPrimary.withValues(alpha: 0.2),
                  ),
                ),
                child: Text(
                  isSatisfied ? 'COMPLETED' : 'REQUIRED',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w800,
                    color: isSatisfied
                        ? const Color(0xFF166534)
                        : AppColors.brandPrimary,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (candidateItems.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text(
                'No items are available in this slot\'s category yet.',
                style: TextStyle(fontSize: 13, color: AppColors.textSecondary),
              ),
            )
          else
            LayoutBuilder(
              builder: (context, constraints) {
                final isTwoCol = constraints.maxWidth >= 480;
                if (isTwoCol) {
                  return Wrap(
                    spacing: 10,
                    runSpacing: 10,
                    children: [
                      for (final item in candidateItems)
                        SizedBox(
                          width: (constraints.maxWidth - 10) / 2,
                          child: _OptionCard(
                            item: item,
                            isSelected: selectedItemIds.contains(item.id),
                            onTap: () => onToggle(item.id),
                            upcharge: slot.substitutionUpchargeAmount,
                          ),
                        ),
                    ],
                  );
                }
                return Column(
                  children: [
                    for (int i = 0; i < candidateItems.length; i++) ...[
                      _OptionCard(
                        item: candidateItems[i],
                        isSelected: selectedItemIds.contains(candidateItems[i].id),
                        onTap: () => onToggle(candidateItems[i].id),
                        upcharge: slot.substitutionUpchargeAmount,
                      ),
                      if (i != candidateItems.length - 1)
                        const SizedBox(height: 8),
                    ],
                  ],
                );
              },
            ),
        ],
      ),
    );
  }
}

class _OptionCard extends StatelessWidget {
  const _OptionCard({
    required this.item,
    required this.isSelected,
    required this.onTap,
    this.upcharge,
  });

  final Item item;
  final bool isSelected;
  final VoidCallback onTap;
  final double? upcharge;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: isSelected ? AppColors.brandPrimaryContainer : AppColors.surface,
      borderRadius: BorderRadius.circular(AppRadius.md),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadius.md),
            border: Border.all(
              color: isSelected ? AppColors.brandPrimary : AppColors.border,
              width: isSelected ? 2 : 1,
            ),
          ),
          child: Row(
            children: [
              Container(
                width: 20,
                height: 20,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  border: Border.all(
                    color: isSelected ? AppColors.brandPrimary : AppColors.border,
                    width: 2,
                  ),
                ),
                child: isSelected
                    ? Center(
                        child: Container(
                          width: 10,
                          height: 10,
                          decoration: const BoxDecoration(
                            shape: BoxShape.circle,
                            color: AppColors.brandPrimary,
                          ),
                        ),
                      )
                    : null,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      item.name,
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight:
                            isSelected ? FontWeight.w700 : FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                decoration: BoxDecoration(
                  color: isSelected
                      ? AppColors.brandPrimary.withValues(alpha: 0.12)
                      : AppColors.background,
                  borderRadius: BorderRadius.circular(AppRadius.sm),
                  border: Border.all(
                    color: isSelected
                        ? AppColors.brandPrimary.withValues(alpha: 0.25)
                        : AppColors.border,
                  ),
                ),
                child: Text(
                  upcharge != null && upcharge! > 0
                      ? '+₱${upcharge!.toStringAsFixed(2)}'
                      : 'INCLUDED',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w700,
                    color: isSelected
                        ? AppColors.brandPrimary
                        : AppColors.textSecondary,
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
