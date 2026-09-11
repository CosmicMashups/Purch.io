import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/domain/item_combo_component_models.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';

/// D2 — the combo/meal builder customization sheet. Each slot
/// (Purch.Domain.Entities.ItemComboComponent) requires picking exactly
/// `quantity` items from its category; a slot with a configured
/// substitutionUpchargeAmount always adds that flat amount to the combo's
/// price once every required slot is filled — the backend prices a slot's
/// upcharge as a flat amount rather than per specific component, see
/// TransactionService.ResolveComboSelectionsAsync's doc comment.
class ComboCustomizationScreen extends ConsumerStatefulWidget {
  const ComboCustomizationScreen({super.key, required this.item});

  final Item item;

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

    final controller = ref.read(cartNotifierProvider.notifier);
    final succeeded = await controller.addLine(
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
      appBar: AppBar(title: Text(widget.item.name)),
      body: slotsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load this combo: $error')),
        data: (slots) {
          if (slots.isEmpty) {
            return const Center(
              child: Text('No slots have been configured for this combo yet.'),
            );
          }

          return itemsAsync.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error:
                (error, stackTrace) =>
                    Center(child: Text('Could not load items: $error')),
            data: (items) {
              return Column(
                children: [
                  Expanded(
                    child: ListView(
                      children: [
                        for (final slot in slots)
                          _SlotSection(
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
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.all(16),
                    child: SizedBox(
                      height: 56,
                      width: double.infinity,
                      child: FilledButton(
                        onPressed:
                            _isComplete(slots) ? () => _addToCart(slots) : null,
                        child: const Text('Add to Cart'),
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
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
          child: Text(
            '${slot.slotLabel} (choose ${slot.quantity})',
            style: Theme.of(context).textTheme.titleMedium,
          ),
        ),
        if (candidateItems.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 16),
            child: Text('No items are available in this slot\'s category yet.'),
          ),
        for (final item in candidateItems)
          CheckboxListTile(
            value: selectedItemIds.contains(item.id),
            onChanged: (_) => onToggle(item.id),
            title: Text(item.name),
          ),
      ],
    );
  }
}
