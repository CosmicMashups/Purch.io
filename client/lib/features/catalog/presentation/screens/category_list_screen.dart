import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../../core/widgets/status_badge.dart';
import '../providers/catalog_providers.dart';
import 'add_category_screen.dart';
import 'edit_category_screen.dart';
import '../../../../core/errors/failure.dart';

/// B5's category half.
class CategoryListScreen extends ConsumerWidget {
  const CategoryListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final categoriesAsync = ref.watch(categoryListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Categories')),
      body: categoriesAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load categories: ${describeError(error)}',
          onRetry: () => ref.read(categoryListProvider.notifier).refresh(),
        ),
        data: (categories) {
          if (categories.isEmpty) {
            return EmptyStateView(
              icon: Icons.category_outlined,
              title: 'No categories yet — tap + to add one.',
              description:
                  'Organize your products into logical groups for faster POS & Kiosk navigation.',
              actionLabel: 'Add Category',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => const AddCategoryScreen(nextSortOrder: 0),
                ),
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(categoryListProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
              itemCount: categories.length,
              separatorBuilder: (_, __) => const SizedBox(height: 8),
              itemBuilder: (context, index) {
                final category = categories[index];
                final colorPair = AppColors.categoryPalette[
                    index % AppColors.categoryPalette.length];

                return Container(
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: AppRadius.mdBorder,
                    boxShadow: AppShadows.subtle,
                    border: Border.all(color: AppColors.border),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 4,
                    ),
                    onTap: () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => EditCategoryScreen(category: category),
                      ),
                    ),
                    leading: Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: colorPair.background,
                        borderRadius: BorderRadius.circular(AppRadius.sm),
                        border: Border.all(color: colorPair.border),
                      ),
                      child: Icon(
                        Icons.category_rounded,
                        color: colorPair.foreground,
                        size: 22,
                      ),
                    ),
                    title: Text(
                      category.name,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: Text(
                      'Sort order: ${category.sortOrder}',
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                    trailing: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        StatusBadge(
                          label: '#${category.sortOrder}',
                          type: StatusBadgeType.info,
                          isSmall: true,
                        ),
                        const SizedBox(width: 8),
                        IconButton(
                          icon: const Icon(
                            Icons.edit_outlined,
                            size: 20,
                            color: AppColors.textSecondary,
                          ),
                          tooltip: 'Edit category',
                          onPressed: () => Navigator.of(context).push<void>(
                            MaterialPageRoute(
                              builder: (_) =>
                                  EditCategoryScreen(category: category),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: Builder(
        builder:
            (context) => FloatingActionButton(
              backgroundColor: AppColors.brandPrimary,
              foregroundColor: AppColors.onBrandPrimary,
              onPressed: () {
                final categories = categoriesAsync.valueOrNull ?? const [];
                Navigator.of(context).push<void>(
                  MaterialPageRoute(
                    builder:
                        (_) =>
                            AddCategoryScreen(nextSortOrder: categories.length),
                  ),
                );
              },
              tooltip: 'Add category',
              child: const Icon(Icons.add),
            ),
      ),
    );
  }
}

