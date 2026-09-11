import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/promo_code_providers.dart';
import 'add_promo_code_screen.dart';

/// Admin/Manager promo code management — the code the cashier types in at
/// D4's cart review. See CartScreen for where they're applied.
class PromoCodeListScreen extends ConsumerWidget {
  const PromoCodeListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final promoCodesAsync = ref.watch(promoCodeListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Promo Codes')),
      body: promoCodesAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load promo codes: $error')),
        data: (promoCodes) {
          if (promoCodes.isEmpty) {
            return const Center(
              child: Text('No promo codes yet — tap + to add one.'),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(promoCodeListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: promoCodes.length,
              itemBuilder: (context, index) {
                final promoCode = promoCodes[index];
                return ListTile(
                  leading: const CircleAvatar(
                    child: Icon(Icons.local_offer_outlined),
                  ),
                  title: Text(promoCode.code),
                  subtitle: Text(promoCode.discountLabel),
                  trailing:
                      promoCode.isActive
                          ? null
                          : const Chip(label: Text('Inactive')),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const AddPromoCodeScreen()),
            ),
        tooltip: 'Add promo code',
        child: const Icon(Icons.add),
      ),
    );
  }
}
