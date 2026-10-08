import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../domain/modifier_models.dart';
import '../providers/catalog_providers.dart';

enum _ApplyMode { category, items }

/// Gives a modifier group to every item of a category, or to the items ticked,
/// so it need not be attached item by item.
class ApplyModifierGroupScreen extends ConsumerStatefulWidget {
  const ApplyModifierGroupScreen({
    super.key,
    required this.groupId,
    required this.groupName,
  });

  final String groupId;
  final String groupName;

  @override
  ConsumerState<ApplyModifierGroupScreen> createState() =>
      _ApplyModifierGroupScreenState();
}

class _ApplyModifierGroupScreenState
    extends ConsumerState<ApplyModifierGroupScreen> {
  _ApplyMode _mode = _ApplyMode.category;
  String? _categoryId;
  final Set<String> _chosen = {};
  String _search = '';
  bool _busy = false;
  String? _message;
  bool _failed = false;

  Future<void> _apply() async {
    setState(() {
      _busy = true;
      _message = null;
    });
    try {
      final result = await ref
          .read(catalogRepositoryProvider)
          .attachModifierGroupToItems(
            widget.groupId,
            _mode == _ApplyMode.category
                ? AttachModifierGroupToItemsRequest(categoryId: _categoryId)
                : AttachModifierGroupToItemsRequest(itemIds: _chosen.toList()),
          );
      if (!mounted) return;
      setState(() {
        _failed = false;
        _message =
            result.alreadyAttached > 0
                ? '${widget.groupName} added to ${result.attached} items; '
                    '${result.alreadyAttached} already had it'
                : '${widget.groupName} added to ${result.attached} items';
        _chosen.clear();
      });
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          _failed = true;
          _message = describeError(error);
        });
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final categories = ref.watch(categoryListProvider).valueOrNull ?? const [];
    final items =
        (ref.watch(itemListProvider).valueOrNull ?? const [])
            .where((item) => item.isActive)
            .toList();
    final shown =
        items
            .where(
              (item) => item.name.toLowerCase().contains(
                _search.trim().toLowerCase(),
              ),
            )
            .toList();
    final categoryCount =
        items.where((item) => item.categoryId == _categoryId).length;
    final ready =
        _mode == _ApplyMode.category
            ? _categoryId != null && categoryCount > 0
            : _chosen.isNotEmpty;

    return Scaffold(
      appBar: AppBar(title: Text('Apply ${widget.groupName}')),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.md),
        children: [
          SegmentedButton<_ApplyMode>(
            segments: const [
              ButtonSegment(
                value: _ApplyMode.category,
                label: Text('All items of a category'),
              ),
              ButtonSegment(
                value: _ApplyMode.items,
                label: Text('Selected items'),
              ),
            ],
            selected: {_mode},
            onSelectionChanged:
                _busy ? null : (value) => setState(() => _mode = value.first),
          ),
          const SizedBox(height: AppSpacing.md),
          if (_mode == _ApplyMode.category)
            DropdownButtonFormField<String?>(
              value: _categoryId,
              isExpanded: true,
              decoration: InputDecoration(
                labelText: 'Category',
                helperText:
                    _categoryId == null
                        ? 'Every active item in it gets the group. Items that already have it are left alone.'
                        : '$categoryCount active ${categoryCount == 1 ? 'item' : 'items'} in this category.',
              ),
              items: [
                for (final category in categories)
                  DropdownMenuItem<String?>(
                    value: category.id,
                    child: Text(category.name),
                  ),
              ],
              onChanged:
                  _busy ? null : (value) => setState(() => _categoryId = value),
            )
          else ...[
            TextField(
              decoration: const InputDecoration(
                labelText: 'Search items',
                prefixIcon: Icon(Icons.search),
              ),
              onChanged: (value) => setState(() => _search = value),
            ),
            const SizedBox(height: AppSpacing.sm),
            if (shown.isEmpty) const Text('No items found.'),
            for (final item in shown)
              CheckboxListTile(
                dense: true,
                title: Text(item.name),
                value: _chosen.contains(item.id),
                onChanged:
                    _busy
                        ? null
                        : (checked) => setState(() {
                          if (checked ?? false) {
                            _chosen.add(item.id);
                          } else {
                            _chosen.remove(item.id);
                          }
                        }),
              ),
            Padding(
              padding: const EdgeInsets.only(top: AppSpacing.sm),
              child: Text('${_chosen.length} selected.'),
            ),
          ],
          const SizedBox(height: AppSpacing.md),
          Align(
            alignment: Alignment.centerLeft,
            child: FilledButton(
              onPressed: _busy || !ready ? null : _apply,
              child: Text(_busy ? 'Applying…' : 'Apply'),
            ),
          ),
          if (_message != null) ...[
            const SizedBox(height: AppSpacing.sm),
            Text(
              _message!,
              style: TextStyle(
                color: _failed ? AppColors.error : AppColors.accentEmerald,
              ),
            ),
          ],
        ],
      ),
    );
  }
}
