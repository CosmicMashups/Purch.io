import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/modifier_models.dart';
import '../providers/catalog_providers.dart';
import 'attach_modifier_group_screen.dart';

/// Restaurant-style item customization (e.g. "Ice Level" on a drink, "No
/// Pickles" on a burger) — shows which modifier groups are attached to this
/// item. Groups themselves are managed tenant-wide in ModifierGroupListScreen
/// and reused across as many items as apply.
class ItemModifierGroupsScreen extends ConsumerWidget {
  const ItemModifierGroupsScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final groupsAsync = ref.watch(itemModifierGroupListProvider(itemId));

    return Scaffold(
      appBar: AppBar(title: Text('Customization: $itemName')),
      body: groupsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load modifier groups: $error')),
        data: (groups) {
          if (groups.isEmpty) {
            return const Center(
              child: Text(
                'No customization options attached yet — tap + to attach one.',
              ),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () =>
                    ref
                        .read(itemModifierGroupListProvider(itemId).notifier)
                        .refresh(),
            child: ListView.builder(
              itemCount: groups.length,
              itemBuilder: (context, index) {
                final group = groups[index];
                return ExpansionTile(
                  leading: const Icon(Icons.tune),
                  title: Text(group.name),
                  subtitle: Text(
                    group.allowMultipleSelection
                        ? 'Multiple selections allowed'
                        : 'Single selection',
                  ),
                  children: [
                    if (group.modifiers.isEmpty)
                      const Padding(
                        padding: EdgeInsets.symmetric(
                          horizontal: 16,
                          vertical: 8,
                        ),
                        child: Text('No options in this group yet.'),
                      )
                    else
                      for (final ItemModifierOption modifier in group.modifiers)
                        ListTile(
                          dense: true,
                          title: Text(modifier.name),
                          trailing: Text(
                            modifier.priceDelta == 0
                                ? 'Free'
                                : '+₱${modifier.priceDelta.toStringAsFixed(2)}',
                          ),
                        ),
                  ],
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(
                builder:
                    (_) => AttachModifierGroupScreen(
                      itemId: itemId,
                      itemName: itemName,
                    ),
              ),
            ),
        tooltip: 'Attach modifier group',
        child: const Icon(Icons.add),
      ),
    );
  }
}
