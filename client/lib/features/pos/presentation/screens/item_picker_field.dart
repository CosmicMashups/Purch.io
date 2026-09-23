import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/formatting/money.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';

/// A tappable field that opens a searchable bottom sheet of catalog items —
/// used by the automatic-promo (BOGO/Combo/Item discount) admin forms to
/// pick trigger/free/bundle items. There's no existing reusable item picker
/// in the codebase (variant_picker_screen.dart picks a variant of an
/// already-chosen item, not an item itself), so this is a small new widget.
class ItemPickerField extends ConsumerWidget {
  const ItemPickerField({
    super.key,
    required this.label,
    required this.selectedItemId,
    required this.onChanged,
  });

  final String label;
  final String? selectedItemId;
  final ValueChanged<Item> onChanged;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final itemsAsync = ref.watch(itemListProvider);

    final selectedName = itemsAsync.maybeWhen(
      data: (items) {
        for (final item in items) {
          if (item.id == selectedItemId) {
            return item.name;
          }
        }
        return null;
      },
      orElse: () => null,
    );

    return InkWell(
      borderRadius: AppRadius.mdBorder,
      onTap: () => _openPicker(context, ref),
      child: InputDecorator(
        decoration: InputDecoration(
          labelText: label,
          isDense: true,
          border: OutlineInputBorder(
            borderRadius: AppRadius.mdBorder,
            borderSide: const BorderSide(color: AppColors.border),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: AppRadius.mdBorder,
            borderSide: const BorderSide(color: AppColors.border),
          ),
          filled: true,
          fillColor: AppColors.cardHover,
        ),
        child: Text(
          selectedName ?? 'Tap to select an item',
          style: TextStyle(
            fontWeight: FontWeight.w600,
            color:
                selectedName == null
                    ? AppColors.textMuted
                    : AppColors.textPrimary,
          ),
        ),
      ),
    );
  }

  Future<void> _openPicker(BuildContext context, WidgetRef ref) async {
    final itemsAsync = ref.read(itemListProvider);
    final items = itemsAsync.valueOrNull ?? const <Item>[];

    final picked = await showModalBottomSheet<Item>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) => _ItemPickerSheet(items: items, title: label),
    );

    if (picked != null) {
      onChanged(picked);
    }
  }
}

class _ItemPickerSheet extends StatefulWidget {
  const _ItemPickerSheet({required this.items, required this.title});

  final List<Item> items;
  final String title;

  @override
  State<_ItemPickerSheet> createState() => _ItemPickerSheetState();
}

class _ItemPickerSheetState extends State<_ItemPickerSheet> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final filtered =
        widget.items
            .where(
              (item) =>
                  item.name.toLowerCase().contains(_query.toLowerCase()),
            )
            .toList();

    return DraggableScrollableSheet(
      initialChildSize: 0.7,
      minChildSize: 0.4,
      maxChildSize: 0.9,
      expand: false,
      builder: (context, scrollController) {
        return Padding(
          padding: EdgeInsets.only(
            bottom: MediaQuery.viewInsetsOf(context).bottom,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 8),
                child: Text(
                  widget.title,
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20),
                child: TextField(
                  autofocus: false,
                  decoration: const InputDecoration(
                    hintText: 'Search items…',
                    prefixIcon: Icon(Icons.search, size: 20),
                    isDense: true,
                  ),
                  onChanged: (value) => setState(() => _query = value),
                ),
              ),
              const SizedBox(height: 8),
              Expanded(
                child:
                    filtered.isEmpty
                        ? const Center(child: Text('No items found'))
                        : ListView.builder(
                          controller: scrollController,
                          itemCount: filtered.length,
                          itemBuilder: (context, index) {
                            final item = filtered[index];
                            return ListTile(
                              title: Text(item.name),
                              trailing: Text(
                                formatCurrency(item.basePrice),
                              ),
                              onTap: () => Navigator.of(context).pop(item),
                            );
                          },
                        ),
              ),
            ],
          ),
        );
      },
    );
  }
}
