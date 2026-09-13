import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
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
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error:
            (error, stackTrace) => Center(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Text(
                  'Could not load modifier groups: $error',
                  style: const TextStyle(color: AppColors.error),
                  textAlign: TextAlign.center,
                ),
              ),
            ),
        data: (groups) {
          if (groups.isEmpty) {
            return Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 72,
                    height: 72,
                    decoration: const BoxDecoration(
                      color: AppColors.cardHover,
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.tune,
                      size: 36,
                      color: AppColors.textMuted,
                    ),
                  ),
                  const SizedBox(height: AppSpacing.md),
                  const Text(
                    'No modifier groups yet — tap + to add one.',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            );
          }

          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh:
                () => ref.read(modifierGroupListProvider.notifier).refresh(),
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
                              ? 'Multiple choices allowed'
                              : 'Single choice only',
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
                      for (final modifier in group.modifiers)
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
                              '+₱${modifier.priceDelta.toStringAsFixed(2)}',
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
                      Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: AppSpacing.md,
                          vertical: AppSpacing.sm,
                        ),
                        child: OutlinedButton.icon(
                          style: OutlinedButton.styleFrom(
                            side: const BorderSide(color: AppColors.border),
                            shape: RoundedRectangleBorder(
                              borderRadius: AppRadius.mdBorder,
                            ),
                          ),
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
                          icon: const Icon(Icons.add, size: 18),
                          label: const Text('Add option'),
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
              MaterialPageRoute(builder: (_) => const AddModifierGroupScreen()),
            ),
        tooltip: 'Add modifier group',
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: AppColors.onBrandPrimary,
        child: const Icon(Icons.add),
      ),
    );
  }
}
