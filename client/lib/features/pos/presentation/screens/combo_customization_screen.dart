import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_combo_component_models.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';

/// D2 — the combo/meal builder customization sheet.
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

  Future<void> _addToCart(List<ItemComboComponent> slots) async {
    final selections = <ComboSelectionRequest>[
      for (final slot in slots)
        for (final itemId in _selectedItemIdsBySlot[slot.id]!)
          ComboSelectionRequest(slotId: slot.id, selectedItemId: itemId),
    ];

    final add =
        widget.addLine ??
        (request) => ref.read(cartNotifierProvider.notifier).addLine(request);
    final succeeded = await add(
      AddTransactionLineRequest(
        itemId: widget.item.id,
        quantity: 1,
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

  @override
  Widget build(BuildContext context) {
    final slotsAsync = ref.watch(
      itemComboComponentListProvider(widget.item.id),
    );
    final itemsAsync = ref.watch(itemListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: Text(widget.item.name)),
      body: slotsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) => Center(
              child: Text(
                'Could not load this combo: $error',
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
                      color: AppColors.cardHover,
                      borderRadius: BorderRadius.circular(AppRadius.lg),
                    ),
                    child: const Icon(
                      Icons.set_meal_outlined,
                      size: 32,
                      color: AppColors.textMuted,
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
                    'Could not load items: $error',
                    style: const TextStyle(color: AppColors.error),
                  ),
                ),
            data: (items) {
              return Column(
                children: [
                  Expanded(
                    child: ListView(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 14,
                      ),
                      children: [
                        for (final slot in slots)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 12),
                            child: _SlotSection(
                              slot: slot,
                              candidateItems:
                                  items
                                      .where(
                                        (item) =>
                                            item.categoryId ==
                                                slot.componentCategoryId &&
                                            item.isActive,
                                      )
                                      .toList(),
                              selectedItemIds:
                                  _selectedItemIdsBySlot[slot.id] ?? const [],
                              onToggle:
                                  (itemId) => _toggleSelection(slot, itemId),
                            ),
                          ),
                      ],
                    ),
                  ),
                  Container(
                    decoration: const BoxDecoration(
                      color: AppColors.surface,
                      boxShadow: AppShadows.card,
                      border: Border(top: BorderSide(color: AppColors.border)),
                    ),
                    padding: const EdgeInsets.all(16),
                    child: SizedBox(
                      height: 52,
                      width: double.infinity,
                      child: FilledButton(
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColors.brandPrimary,
                          foregroundColor: AppColors.onBrandPrimary,
                          shape: const RoundedRectangleBorder(
                            borderRadius: AppRadius.mdBorder,
                          ),
                        ),
                        onPressed:
                            _isComplete(slots) ? () => _addToCart(slots) : null,
                        child: const Text(
                          'Add to Cart',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                    ),
                  ),
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
    required this.slot,
    required this.candidateItems,
    required this.selectedItemIds,
    required this.onToggle,
  });

  final ItemComboComponent slot;
  final List<Item> candidateItems;
  final List<String> selectedItemIds;
  final ValueChanged<String> onToggle;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        boxShadow: AppShadows.subtle,
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.symmetric(vertical: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  '${slot.slotLabel} (choose ${slot.quantity})',
                  style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                if (slot.substitutionUpchargeAmount != null)
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.accentWarmContainer,
                      borderRadius: BorderRadius.circular(AppRadius.sm),
                    ),
                    child: Text(
                      '+₱${slot.substitutionUpchargeAmount!.toStringAsFixed(2)}',
                      style: const TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                        color: AppColors.onAccentWarmContainer,
                      ),
                    ),
                  ),
              ],
            ),
          ),
          if (candidateItems.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Text(
                'No items are available in this slot\'s category yet.',
                style: TextStyle(fontSize: 13, color: AppColors.textSecondary),
              ),
            ),
          for (final item in candidateItems)
            CheckboxListTile(
              value: selectedItemIds.contains(item.id),
              activeColor: AppColors.brandPrimary,
              onChanged: (_) => onToggle(item.id),
              title: Text(
                item.name,
                style: const TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ),
        ],
      ),
    );
  }
}

