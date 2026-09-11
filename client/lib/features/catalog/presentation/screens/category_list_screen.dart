import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/catalog_providers.dart';
import 'add_category_screen.dart';

/// B5's category half (modifier groups are separate, not yet built).
class CategoryListScreen extends ConsumerWidget {
  const CategoryListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final categoriesAsync = ref.watch(categoryListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Categories')),
      body: categoriesAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load categories: $error')),
        data: (categories) {
          if (categories.isEmpty) {
            return const Center(
              child: Text('No categories yet — tap + to add one.'),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(categoryListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: categories.length,
              itemBuilder: (context, index) {
                final category = categories[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.category)),
                  title: Text(category.name),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: Builder(
        builder:
            (context) => FloatingActionButton(
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
