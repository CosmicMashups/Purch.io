import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/catalog_providers.dart';
import 'add_combo_component_screen.dart';

/// B4 — combo/meal builder slots. Only reachable for items whose pricingType
/// is combo (see ItemListScreen), matching the backend's own rejection of
/// combo slots against any other pricing type.
class ComboComponentsScreen extends ConsumerWidget {
  const ComboComponentsScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final componentsAsync = ref.watch(itemComboComponentListProvider(itemId));

    return Scaffold(
      appBar: AppBar(title: Text('Combo Slots: $itemName')),
      body: componentsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load combo slots: $error')),
        data: (components) {
          if (components.isEmpty) {
            return const Center(
              child: Text('No combo slots yet — tap + to add one.'),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () =>
                    ref
                        .read(itemComboComponentListProvider(itemId).notifier)
                        .refresh(),
            child: ListView.builder(
              itemCount: components.length,
              itemBuilder: (context, index) {
                final component = components[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.set_meal)),
                  title: Text(component.slotLabel),
                  subtitle: Text(
                    'From ${component.componentCategoryName} · qty ${component.quantity}'
                    '${component.substitutionUpchargeAmount != null ? ' · +₱${component.substitutionUpchargeAmount!.toStringAsFixed(2)} to substitute' : ''}',
                  ),
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
                    (_) => AddComboComponentScreen(
                      itemId: itemId,
                      itemName: itemName,
                    ),
              ),
            ),
        tooltip: 'Add combo slot',
        child: const Icon(Icons.add),
      ),
    );
  }
}
