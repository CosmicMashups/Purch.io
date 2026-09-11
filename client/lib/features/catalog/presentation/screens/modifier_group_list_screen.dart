import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/catalog_providers.dart';
import 'add_modifier_group_screen.dart';
import 'add_modifier_screen.dart';

/// B5's modifier groups half (categories are on their own screen).
class ModifierGroupListScreen extends ConsumerWidget {
  const ModifierGroupListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final groupsAsync = ref.watch(modifierGroupListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Modifier Groups')),
      body: groupsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load modifier groups: $error')),
        data: (groups) {
          if (groups.isEmpty) {
            return const Center(
              child: Text('No modifier groups yet — tap + to add one.'),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () => ref.read(modifierGroupListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: groups.length,
              itemBuilder: (context, index) {
                final group = groups[index];
                return ExpansionTile(
                  title: Text(group.name),
                  subtitle: Text(
                    [
                      group.allowMultipleSelection
                          ? 'Multiple choices allowed'
                          : 'Single choice only',
                      if (group.isRequired) 'Required',
                    ].join(' · '),
                  ),
                  children: [
                    for (final modifier in group.modifiers)
                      ListTile(
                        title: Text(modifier.name),
                        trailing: Text(
                          '+₱${modifier.priceDelta.toStringAsFixed(2)}',
                        ),
                      ),
                    Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 8,
                      ),
                      child: OutlinedButton.icon(
                        onPressed:
                            () => Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder:
                                    (_) => AddModifierScreen(
                                      groupId: group.id,
                                      groupName: group.name,
                                    ),
                              ),
                            ),
                        icon: const Icon(Icons.add),
                        label: const Text('Add option'),
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
              MaterialPageRoute(builder: (_) => const AddModifierGroupScreen()),
            ),
        tooltip: 'Add modifier group',
        child: const Icon(Icons.add),
      ),
    );
  }
}
