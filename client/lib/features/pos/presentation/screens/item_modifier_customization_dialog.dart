import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_fonts/google_fonts.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/domain/item_variant_models.dart';
import '../../../catalog/domain/modifier_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';

/// Modal dialog allowing the cashier or kiosk customer to pick modifier options
/// (sugar level, toppings, add-ons, etc.) for an item or item variant before adding to cart.
class ItemModifierCustomizationDialog extends ConsumerStatefulWidget {
  const ItemModifierCustomizationDialog({
    super.key,
    required this.item,
    this.itemVariant,
    this.addLine,
  });

  final Item item;
  final ItemVariant? itemVariant;

  /// Overrides how an add-to-cart is performed — defaults to the POS cart
  /// (cartNotifierProvider) when omitted.
  final Future<bool> Function(AddTransactionLineRequest)? addLine;

  @override
  ConsumerState<ItemModifierCustomizationDialog> createState() =>
      _ItemModifierCustomizationDialogState();
}

class _ItemModifierCustomizationDialogState
    extends ConsumerState<ItemModifierCustomizationDialog> {
  /// Maps modifierGroupId -> Set of selected ItemModifierOption IDs.
  final Map<String, Set<String>> _selectedModifierIdsByGroup = {};
  int _quantity = 1;
  bool _isSubmitting = false;

  void _selectSingle(String groupId, String modifierId) {
    setState(() {
      final set = _selectedModifierIdsByGroup.putIfAbsent(groupId, () => <String>{});
      set.clear();
      set.add(modifierId);
    });
  }

  void _toggleMultiple(String groupId, String modifierId) {
    setState(() {
      final set = _selectedModifierIdsByGroup.putIfAbsent(groupId, () => <String>{});
      if (set.contains(modifierId)) {
        set.remove(modifierId);
      } else {
        set.add(modifierId);
      }
    });
  }

  bool _isValid(List<ModifierGroup> groups) {
    for (final group in groups) {
      if (group.isRequired) {
        final selected = _selectedModifierIdsByGroup[group.id];
        if (selected == null || selected.isEmpty) {
          return false;
        }
      }
    }
    return true;
  }

  double _calculateUnitPrice(List<ModifierGroup> groups) {
    final basePrice = widget.itemVariant?.priceOverride ?? widget.item.basePrice;
    double deltaSum = 0.0;

    for (final group in groups) {
      final selected = _selectedModifierIdsByGroup[group.id];
      if (selected != null && selected.isNotEmpty) {
        for (final modifier in group.modifiers) {
          if (selected.contains(modifier.id)) {
            deltaSum += modifier.priceDelta;
          }
        }
      }
    }

    return basePrice + deltaSum;
  }

  List<String> _collectSelectedModifierIds() {
    final ids = <String>[];
    for (final set in _selectedModifierIdsByGroup.values) {
      ids.addAll(set);
    }
    return ids;
  }

  Future<void> _submit(List<ModifierGroup> groups) async {
    if (!_isValid(groups) || _isSubmitting) return;

    setState(() => _isSubmitting = true);

    final selectedIds = _collectSelectedModifierIds();
    final add =
        widget.addLine ??
        (request) => ref.read(cartNotifierProvider.notifier).addLine(request);

    final succeeded = await add(
      AddTransactionLineRequest(
        itemId: widget.item.id,
        itemVariantId: widget.itemVariant?.id,
        quantity: _quantity.toDouble(),
        selectedModifierIds: selectedIds.isNotEmpty ? selectedIds : null,
      ),
    );

    if (mounted) {
      setState(() => _isSubmitting = false);
      if (succeeded) {
        Navigator.of(context).pop(true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final groupsAsync = ref.watch(itemModifierGroupListProvider(widget.item.id));

    return Dialog(
      backgroundColor: AppColors.surface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      insetPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 520, maxHeight: 720),
        child: groupsAsync.when(
          loading:
              () => const Padding(
                padding: EdgeInsets.all(48),
                child: Center(child: CircularProgressIndicator()),
              ),
          error:
              (error, _) => Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.error_outline, color: AppColors.error, size: 40),
                    const SizedBox(height: 12),
                    Text(
                      'Could not load modifier groups: ${describeError(error)}',
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: AppColors.error),
                    ),
                    const SizedBox(height: 16),
                    TextButton(
                      onPressed: () => Navigator.of(context).pop(false),
                      child: const Text('Close'),
                    ),
                  ],
                ),
              ),
          data: (groups) {
            final unitPrice = _calculateUnitPrice(groups);
            final totalPrice = unitPrice * _quantity;
            final canAdd = _isValid(groups) && !_isSubmitting;

            return Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Header
                Padding(
                  padding: const EdgeInsets.fromLTRB(20, 20, 16, 12),
                  child: Row(
                    children: [
                      Container(
                        width: 44,
                        height: 44,
                        decoration: BoxDecoration(
                          color: AppColors.brandPrimaryContainer,
                          borderRadius: BorderRadius.circular(AppRadius.md),
                        ),
                        child: const Icon(
                          Icons.tune_rounded,
                          color: AppColors.brandPrimary,
                          size: 24,
                        ),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              widget.item.name,
                              style: GoogleFonts.outfit(
                                fontSize: 18,
                                fontWeight: FontWeight.w800,
                                color: AppColors.textPrimary,
                              ),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                            if (widget.itemVariant != null)
                              Text(
                                widget.itemVariant!.attributesLabel,
                                style: const TextStyle(
                                  fontSize: 12,
                                  color: AppColors.brandPrimary,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                          ],
                        ),
                      ),
                      IconButton(
                        onPressed: () => Navigator.of(context).pop(false),
                        icon: const Icon(
                          Icons.close_rounded,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
                const Divider(color: AppColors.border, height: 1),

                // Modifier groups list
                Flexible(
                  child: ListView.separated(
                    padding: const EdgeInsets.all(20),
                    shrinkWrap: true,
                    itemCount: groups.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 18),
                    itemBuilder: (context, index) {
                      final group = groups[index];
                      final selected =
                          _selectedModifierIdsByGroup[group.id] ?? const <String>{};

                      return Container(
                        decoration: BoxDecoration(
                          color: AppColors.background,
                          borderRadius: BorderRadius.circular(AppRadius.lg),
                          border: Border.all(color: AppColors.border),
                        ),
                        padding: const EdgeInsets.all(14),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Expanded(
                                  child: Text(
                                    group.name,
                                    style: GoogleFonts.plusJakartaSans(
                                      fontSize: 14,
                                      fontWeight: FontWeight.w700,
                                      color: AppColors.textPrimary,
                                    ),
                                  ),
                                ),
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 8,
                                    vertical: 2,
                                  ),
                                  decoration: BoxDecoration(
                                    color: group.isRequired
                                        ? (selected.isEmpty
                                            ? AppColors.error.withValues(alpha: 0.12)
                                            : AppColors.accentEmerald.withValues(alpha: 0.12))
                                        : AppColors.border.withValues(alpha: 0.5),
                                    borderRadius: BorderRadius.circular(AppRadius.full),
                                  ),
                                  child: Text(
                                    group.isRequired
                                        ? (selected.isEmpty ? 'Required' : 'Selected')
                                        : 'Optional',
                                    style: TextStyle(
                                      fontSize: 10,
                                      fontWeight: FontWeight.w700,
                                      color: group.isRequired
                                          ? (selected.isEmpty
                                              ? AppColors.error
                                              : AppColors.accentEmerald)
                                          : AppColors.textSecondary,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 10),
                            Wrap(
                              spacing: 8,
                              runSpacing: 8,
                              children: [
                                for (final option in group.modifiers)
                                  _buildOptionChip(
                                    option: option,
                                    isSelected: selected.contains(option.id),
                                    allowMultiple: group.allowMultipleSelection,
                                    onTap: () {
                                      if (group.allowMultipleSelection) {
                                        _toggleMultiple(group.id, option.id);
                                      } else {
                                        _selectSingle(group.id, option.id);
                                      }
                                    },
                                  ),
                              ],
                            ),
                          ],
                        ),
                      );
                    },
                  ),
                ),

                const Divider(color: AppColors.border, height: 1),

                // Footer: Quantity Stepper + Live Total + Add CTA
                Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          // Quantity stepper
                          Container(
                            decoration: BoxDecoration(
                              color: AppColors.background,
                              borderRadius: BorderRadius.circular(10),
                              border: Border.all(color: AppColors.border),
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                IconButton(
                                  visualDensity: VisualDensity.compact,
                                  icon: const Icon(Icons.remove, size: 18),
                                  onPressed:
                                      _quantity > 1
                                          ? () => setState(() => _quantity--)
                                          : null,
                                ),
                                Text(
                                  '$_quantity',
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w800,
                                    fontSize: 15,
                                  ),
                                ),
                                IconButton(
                                  visualDensity: VisualDensity.compact,
                                  icon: const Icon(Icons.add, size: 18),
                                  onPressed: () => setState(() => _quantity++),
                                ),
                              ],
                            ),
                          ),

                          // Total
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.end,
                            children: [
                              Text(
                                'Total Amount',
                                style: GoogleFonts.plusJakartaSans(
                                  fontSize: 11,
                                  fontWeight: FontWeight.w600,
                                  color: AppColors.textSecondary,
                                ),
                              ),
                              Text(
                                '₱${totalPrice.toStringAsFixed(2)}',
                                style: GoogleFonts.jetBrainsMono(
                                  fontSize: 20,
                                  fontWeight: FontWeight.w800,
                                  color: AppColors.brandPrimary,
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      SizedBox(
                        width: double.infinity,
                        height: 48,
                        child: FilledButton(
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            foregroundColor: Colors.white,
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(12),
                            ),
                          ),
                          onPressed: canAdd ? () => _submit(groups) : null,
                          child: _isSubmitting
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : Text(
                                  canAdd
                                      ? 'Add to Cart — ₱${totalPrice.toStringAsFixed(2)}'
                                      : 'Select Required Options',
                                  style: const TextStyle(
                                    fontSize: 15,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            );
          },
        ),
      ),
    );
  }

  Widget _buildOptionChip({
    required ItemModifierOption option,
    required bool isSelected,
    required bool allowMultiple,
    required VoidCallback onTap,
  }) {
    final priceText = option.priceDelta > 0
        ? ' (+₱${option.priceDelta.toStringAsFixed(2)})'
        : (option.priceDelta < 0
            ? ' (-₱${(-option.priceDelta).toStringAsFixed(2)})'
            : '');

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(10),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        decoration: BoxDecoration(
          color: isSelected
              ? AppColors.brandPrimaryContainer
              : AppColors.surface,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(
            color: isSelected
                ? AppColors.brandPrimary
                : AppColors.border,
            width: isSelected ? 1.5 : 1.0,
          ),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              allowMultiple
                  ? (isSelected ? Icons.check_box_rounded : Icons.check_box_outline_blank_rounded)
                  : (isSelected ? Icons.radio_button_checked_rounded : Icons.radio_button_unchecked_rounded),
              size: 16,
              color: isSelected ? AppColors.brandPrimary : AppColors.textSecondary,
            ),
            const SizedBox(width: 6),
            Text(
              '${option.name}$priceText',
              style: TextStyle(
                fontSize: 13,
                fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
                color: isSelected ? AppColors.brandPrimary : AppColors.textPrimary,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
