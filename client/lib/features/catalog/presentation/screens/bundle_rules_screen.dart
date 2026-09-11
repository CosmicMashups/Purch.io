import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/catalog_providers.dart';
import 'add_bundle_rule_screen.dart';

/// B2b — bundle promo rules. Only reachable for items whose pricingType is
/// bundle (see ItemListScreen), matching the backend's own rejection of
/// bundle rules against any other pricing type.
class BundleRulesScreen extends ConsumerWidget {
  const BundleRulesScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final rulesAsync = ref.watch(bundleRuleListProvider(itemId));

    return Scaffold(
      appBar: AppBar(title: Text('Bundle Rules: $itemName')),
      body: rulesAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load bundle rules: $error')),
        data: (rules) {
          if (rules.isEmpty) {
            return const Center(
              child: Text('No bundle rules yet — tap + to add one.'),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () =>
                    ref.read(bundleRuleListProvider(itemId).notifier).refresh(),
            child: ListView.builder(
              itemCount: rules.length,
              itemBuilder: (context, index) {
                final rule = rules[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.local_offer)),
                  title: Text(rule.description),
                  subtitle: Text(
                    'Buy ${rule.triggerQuantity} for ₱${rule.bundlePrice.toStringAsFixed(2)}'
                    '${rule.isActive ? '' : ' · inactive'}',
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
                    (_) =>
                        AddBundleRuleScreen(itemId: itemId, itemName: itemName),
              ),
            ),
        tooltip: 'Add bundle rule',
        child: const Icon(Icons.add),
      ),
    );
  }
}
