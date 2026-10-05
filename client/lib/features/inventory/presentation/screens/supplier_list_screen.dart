import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/supplier_providers.dart';
import 'add_supplier_screen.dart';
import '../../../../core/errors/failure.dart';

/// C4's supplier directory.
class SupplierListScreen extends ConsumerWidget {
  const SupplierListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final suppliersAsync = ref.watch(supplierListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Suppliers'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
      ),
      body: suppliersAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load suppliers: ${describeError(error)}',
          onRetry: () => ref.read(supplierListProvider.notifier).refresh(),
        ),
        data: (suppliers) {
          if (suppliers.isEmpty) {
            return EmptyStateView(
              icon: Icons.local_shipping_outlined,
              title: 'No suppliers yet — tap + to add one.',
              description:
                  'Manage vendor contacts, order lead times, and preferred suppliers.',
              actionLabel: 'Add Supplier',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(builder: (_) => const AddSupplierScreen()),
              ),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh: () => ref.read(supplierListProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.lg),
              itemCount: suppliers.length,
              separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                final supplier = suppliers[index];
                return Card(
                  elevation: 0,
                  color: AppColors.surface,
                  shape: RoundedRectangleBorder(
                    borderRadius: AppRadius.mdBorder,
                    side: const BorderSide(color: AppColors.border),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.xs,
                    ),
                    leading: Container(
                      padding: const EdgeInsets.all(AppSpacing.sm),
                      decoration: BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        borderRadius: AppRadius.mdBorder,
                      ),
                      child: const Icon(
                        Icons.local_shipping,
                        color: AppColors.brandPrimary,
                        size: 22,
                      ),
                    ),
                    title: Text(
                      supplier.name,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    onTap: () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => AddSupplierScreen(supplier: supplier),
                      ),
                    ),
                    trailing: const Icon(
                      Icons.edit_outlined,
                      size: 18,
                      color: AppColors.textSecondary,
                    ),
                    subtitle: () {
                      final lines = [
                        if (supplier.specialization != null) supplier.specialization!,
                        if (supplier.address != null) supplier.address!,
                        if (supplier.contactInfo != null) supplier.contactInfo!,
                      ];
                      if (lines.isEmpty) {
                        return null;
                      }
                      return Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Text(
                          lines.join('\n'),
                          style: const TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 13,
                          ),
                        ),
                      );
                    }(),
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: Colors.white,
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
