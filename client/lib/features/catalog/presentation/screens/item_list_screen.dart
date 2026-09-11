import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/catalog_providers.dart';
import 'add_item_screen.dart';

/// B1's item catalog list.
class ItemListScreen extends ConsumerWidget {
  const ItemListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final itemsAsync = ref.watch(itemListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Items')),
      body: itemsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load items: $error')),
        data: (items) {
          if (items.isEmpty) {
            return const Center(
              child: Text('No items yet — tap + to add your first one.'),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(itemListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: items.length,
              itemBuilder: (context, index) {
                final item = items[index];
                return ListTile(
                  leading: CircleAvatar(
                    child: Icon(
                      item.isActive
                          ? Icons.inventory_2
                          : Icons.inventory_2_outlined,
                    ),
                  ),
                  title: Text(item.name),
                  subtitle: Text('₱${item.basePrice.toStringAsFixed(2)}'),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const AddItemScreen()),
            ),
        tooltip: 'Add item',
        child: const Icon(Icons.add),
      ),
    );
  }
}
