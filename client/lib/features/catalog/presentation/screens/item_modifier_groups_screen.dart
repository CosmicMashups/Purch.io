import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../domain/modifier_models.dart';
import '../providers/catalog_providers.dart';
import 'attach_modifier_group_screen.dart';
import '../../../../core/errors/failure.dart';

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
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load modifier groups: ${describeError(error)}',
          onRetry: () =>
              ref.read(itemModifierGroupListProvider(itemId).notifier).refresh(),
        ),
        data: (groups) {
          if (groups.isEmpty) {
            return EmptyStateView(
              icon: Icons.tune_rounded,
              title:
                  'No customization options attached yet — tap + to attach one.',
              description:
                  'Attach add-ons like extra sauce, sugar levels, or toppings to this item.',
              actionLabel: 'Attach Option Group',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => AttachModifierGroupScreen(
                    itemId: itemId,
                    itemName: itemName,
                  ),
                ),
              ),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh:
                () =>
                    ref
                        .read(itemModifierGroupListProvider(itemId).notifier)
                        .refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(AppSpacing.md),
              itemCount: groups.length,
              separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
              itemBuilder: (context, index) {
                final group = groups[index];
                return Container(
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: AppRadius.lgBorder,
                    border: Border.all(color: AppColors.border),
                    boxShadow: AppShadows.subtle,
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: ExpansionTile(
                    shape: const Border(),
                    collapsedShape: const Border(),
                    leading: Container(
                      width: 42,
                      height: 42,
                      decoration: BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        borderRadius: BorderRadius.circular(AppRadius.full),
                      ),
                      child: const Icon(
                        Icons.tune,
                        size: 20,
                        color: AppColors.brandPrimary,
                      ),
                    ),
                    title: Text(
                      group.name,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 16,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text(
                        [
                          group.allowMultipleSelection
                              ? 'Multiple selections allowed'
                              : 'Single selection',
                          if (group.isRequired) 'Required',
                        ].join(' · '),
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: group.isRequired ? FontWeight.w600 : FontWeight.w400,
                          color: group.isRequired ? AppColors.accentWarm : AppColors.textSecondary,
                        ),
                      ),
                    ),
                    children: [
                      const Divider(height: 1, color: AppColors.borderSubtle),
                      if (group.modifiers.isEmpty)
                        const Padding(
                          padding: EdgeInsets.symmetric(
                            horizontal: AppSpacing.md,
                            vertical: AppSpacing.md,
                          ),
                          child: Text(
                            'No options in this group yet.',
                            style: TextStyle(color: AppColors.textSecondary),
                          ),
                        )
                      else
                        for (final ItemModifierOption modifier in group.modifiers)
                          ListTile(
                            dense: true,
                            contentPadding: const EdgeInsets.symmetric(
                              horizontal: AppSpacing.md,
                              vertical: 2,
                            ),
                            title: Text(
                              modifier.name,
                              style: const TextStyle(
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                            ),
                            trailing: Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: AppSpacing.sm,
                                vertical: 4,
                              ),
                              decoration: BoxDecoration(
                                color: modifier.priceDelta > 0
                                    ? AppColors.accentEmeraldContainer
                                    : AppColors.cardHover,
                                borderRadius: BorderRadius.circular(AppRadius.full),
                              ),
                              child: Text(
                                modifier.priceDelta == 0
                                    ? 'Free'
                                    : '+₱${modifier.priceDelta.toStringAsFixed(2)}',
                                style: TextStyle(
                                  fontWeight: FontWeight.w700,
                                  fontSize: 13,
                                  fontFeatures: const [FontFeature.tabularFigures()],
                                  color: modifier.priceDelta > 0
                                      ? AppColors.accentEmerald
                                      : AppColors.textSecondary,
                                ),
                              ),
                            ),
                          ),
                    ],
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
                    (_) => AttachModifierGroupScreen(
                      itemId: itemId,
                      itemName: itemName,
                    ),
              ),
            ),
        tooltip: 'Attach modifier group',
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: AppColors.onBrandPrimary,
        child: const Icon(Icons.add),
      ),
    );
  }
}
