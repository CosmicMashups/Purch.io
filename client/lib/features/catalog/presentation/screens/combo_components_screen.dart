import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/formatting/money.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/catalog_providers.dart';
import 'add_combo_component_screen.dart';
import '../../../../core/errors/failure.dart';

/// B4 — combo components / slots for a combo item.
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
      backgroundColor: AppColors.background,
      appBar: AppBar(title: Text('Combo Slots: $itemName')),
      body: componentsAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load combo slots: ${describeError(error)}',
          onRetry: () => ref
              .read(itemComboComponentListProvider(itemId).notifier)
              .refresh(),
        ),
        data: (components) {
          if (components.isEmpty) {
            return EmptyStateView(
              icon: Icons.set_meal_outlined,
              title: 'No combo slots yet — tap + to add one.',
              description:
                  'Define choice slots (e.g. Drink, Side, Main) and allowed item options.',
              actionLabel: 'Add Combo Slot',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => AddComboComponentScreen(
                    itemId: itemId,
                    itemName: itemName,
                  ),
                ),
              ),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () =>
                    ref
                        .read(itemComboComponentListProvider(itemId).notifier)
                        .refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
              itemCount: components.length,
              separatorBuilder: (_, __) => const SizedBox(height: 8),
              itemBuilder: (context, index) {
                final component = components[index];
                return Container(
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: AppRadius.mdBorder,
                    boxShadow: AppShadows.subtle,
                    border: Border.all(color: AppColors.border),
                  ),
                  child: ListTile(
                    leading: Container(
                      width: 40,
                      height: 40,
                      decoration: BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        borderRadius: BorderRadius.circular(AppRadius.sm),
                      ),
                      child: const Icon(
                        Icons.set_meal_rounded,
                        color: AppColors.brandPrimary,
                        size: 20,
                      ),
                    ),
                    title: Text(
                      component.slotLabel,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: Text(
                      'From ${component.componentCategoryName} · qty ${component.quantity}'
                      '${component.substitutionUpchargeAmount != null ? ' · +${formatCurrency(component.substitutionUpchargeAmount!)} to substitute' : ''}',
                      style: const TextStyle(
                        fontSize: 13,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: AppColors.onBrandPrimary,
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

