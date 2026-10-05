import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/formatting/money.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../domain/modifier_models.dart';
import '../providers/catalog_providers.dart';

/// Links a modifier group to a category (or unlinks it) and tunes each of that
/// category's items for this group: a price of the group's own, or hiding it.
class ModifierGroupCategoryScreen extends ConsumerStatefulWidget {
  const ModifierGroupCategoryScreen({super.key, required this.groupId});

  final String groupId;

  @override
  ConsumerState<ModifierGroupCategoryScreen> createState() =>
      _ModifierGroupCategoryScreenState();
}

class _ModifierGroupCategoryScreenState
    extends ConsumerState<ModifierGroupCategoryScreen> {
  String? _categoryId;
  bool _loadedCategory = false;
  bool _busy = false;
  String? _error;

  Future<void> _run(Future<void> Function() action) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await action();
      await ref.read(modifierGroupListProvider.notifier).refresh();
    } on Object catch (error) {
      if (mounted) setState(() => _error = describeError(error));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final groups = ref.watch(modifierGroupListProvider).valueOrNull ?? const [];
    final group = groups.where((g) => g.id == widget.groupId).firstOrNull;
    final categories = ref.watch(categoryListProvider).valueOrNull ?? const [];
    final repository = ref.read(catalogRepositoryProvider);

    if (group == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Category & prices')),
        body: const Center(child: CircularProgressIndicator()),
      );
    }
    if (!_loadedCategory) {
      _categoryId = group.categoryId;
      _loadedCategory = true;
    }

    return Scaffold(
      appBar: AppBar(title: Text(group.name)),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.md),
        children: [
          DropdownButtonFormField<String?>(
            value: _categoryId,
            isExpanded: true,
            decoration: const InputDecoration(
              labelText: 'Category offered in this group',
              helperText:
                  'Every active item in it is offered. Changing it clears the prices below.',
            ),
            items: [
              const DropdownMenuItem<String?>(value: null, child: Text('None')),
              for (final category in categories)
                DropdownMenuItem<String?>(
                  value: category.id,
                  child: Text(category.name),
                ),
            ],
            onChanged: _busy ? null : (value) => setState(() => _categoryId = value),
          ),
          const SizedBox(height: AppSpacing.sm),
          Align(
            alignment: Alignment.centerLeft,
            child: FilledButton(
              onPressed:
                  _busy || _categoryId == group.categoryId
                      ? null
                      : () => _run(
                        () => repository.updateModifierGroup(
                          group.id,
                          UpdateModifierGroupRequest(
                            name: group.name,
                            allowMultipleSelection: group.allowMultipleSelection,
                            isRequired: group.isRequired,
                            categoryId: _categoryId,
                          ),
                        ),
                      ),
              child: const Text('Save category'),
            ),
          ),
          if (_error != null) ...[
            const SizedBox(height: AppSpacing.sm),
            Text(_error!, style: const TextStyle(color: AppColors.error)),
          ],
          const SizedBox(height: AppSpacing.md),
          if (group.categoryId != null && group.categoryItems.isEmpty)
            const Text('This category has no active items yet.'),
          for (final item in group.categoryItems)
            _CategoryItemTile(
              key: ValueKey(
                '${item.itemId}:${item.priceOverride}:${item.isExcluded}',
              ),
              item: item,
              busy: _busy,
              onSave:
                  (price, hidden) => _run(
                    () => repository.updateModifierCategoryItem(
                      group.id,
                      item.itemId,
                      UpdateModifierCategoryItemRequest(
                        priceOverride: price,
                        isExcluded: hidden,
                      ),
                    ),
                  ),
            ),
        ],
      ),
    );
  }
}

class _CategoryItemTile extends StatefulWidget {
  const _CategoryItemTile({
    super.key,
    required this.item,
    required this.busy,
    required this.onSave,
  });

  final ModifierCategoryItem item;
  final bool busy;
  final void Function(double? price, bool hidden) onSave;

  @override
  State<_CategoryItemTile> createState() => _CategoryItemTileState();
}

class _CategoryItemTileState extends State<_CategoryItemTile> {
  late final TextEditingController _price = TextEditingController(
    text: widget.item.priceOverride?.toString() ?? '',
  );
  late bool _hidden = widget.item.isExcluded;

  @override
  void dispose() {
    _price.dispose();
    super.dispose();
  }

  double? get _parsedPrice =>
      _price.text.trim().isEmpty ? null : double.tryParse(_price.text.trim());

  bool get _changed =>
      _parsedPrice != widget.item.priceOverride ||
      _hidden != widget.item.isExcluded;

  @override
  Widget build(BuildContext context) {
    final item = widget.item;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.sm),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    item.name,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                  Text('Item price ${formatCurrency(item.basePrice)}'),
                ],
              ),
            ),
            SizedBox(
              width: 96,
              child: TextField(
                controller: _price,
                enabled: !widget.busy,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(
                  labelText: 'Price here',
                  isDense: true,
                ),
                onChanged: (_) => setState(() {}),
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text('Hide', style: TextStyle(fontSize: 12)),
                Switch(
                  value: _hidden,
                  onChanged:
                      widget.busy ? null : (v) => setState(() => _hidden = v),
                ),
              ],
            ),
            IconButton(
              tooltip: 'Save ${item.name}',
              icon: const Icon(Icons.check),
              onPressed:
                  widget.busy || !_changed
                      ? null
                      : () => widget.onSave(_parsedPrice, _hidden),
            ),
          ],
        ),
      ),
    );
  }
}
