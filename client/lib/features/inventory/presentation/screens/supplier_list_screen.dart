import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/supplier_providers.dart';
import 'add_supplier_screen.dart';

/// C5 — the supplier list a purchase order is created against.
class SupplierListScreen extends ConsumerWidget {
  const SupplierListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final suppliersAsync = ref.watch(supplierListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Suppliers')),
      body: suppliersAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load suppliers: $error')),
        data: (suppliers) {
          if (suppliers.isEmpty) {
            return const Center(
              child: Text('No suppliers yet — tap + to add one.'),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(supplierListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: suppliers.length,
              itemBuilder: (context, index) {
                final supplier = suppliers[index];
                return ListTile(
                  leading: const CircleAvatar(
                    child: Icon(Icons.local_shipping),
                  ),
                  title: Text(supplier.name),
                  subtitle:
                      supplier.contactInfo != null
                          ? Text(supplier.contactInfo!)
                          : null,
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const AddSupplierScreen()),
            ),
        tooltip: 'Add supplier',
        child: const Icon(Icons.add),
      ),
    );
  }
}
