import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../providers/kiosk_providers.dart';
import 'kiosk_cart_screen.dart';
import 'kiosk_item_list_screen.dart';
import '../../../../core/errors/failure.dart';

/// E2 — Kiosk category browsing screen.
/// Large, tactile 2-column cards with responsive tap targets and order bag badge.
class KioskCategoryScreen extends ConsumerWidget {
  const KioskCategoryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final categoriesAsync = ref.watch(categoryListProvider);
    final cartAsync = ref.watch(kioskCartNotifierProvider);
    final itemCount = cartAsync.valueOrNull?.itemCount ?? 0;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Choose a Category'),
        backgroundColor: AppColors.surface,
        centerTitle: true,
        elevation: 0,
        scrolledUnderElevation: 1,
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 12),
            child: IconButton(
              onPressed:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => const KioskCartScreen()),
                  ),
              icon: Badge(
                backgroundColor: AppColors.accentWarm,
                textColor: Colors.white,
                label: Text(
                  '',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
                isLabelVisible: itemCount > 0,
                child: Container(
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: AppColors.brandPrimaryContainer,
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.shopping_bag_outlined,
                    color: AppColors.brandPrimary,
                    size: 24,
                  ),
                ),
              ),
              tooltip: 'View your order',
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: categoriesAsync.when(
          loading: () => const Center(
            child: CircularProgressIndicator(color: AppColors.brandPrimary),
          ),
          error: (error, stackTrace) => ErrorStateView(
            message: 'Could not load the menu: ${describeError(error)}',
            onRetry: () => ref.read(categoryListProvider.notifier).refresh(),
          ),
          data: (categories) {
            if (categories.isEmpty) {
              return EmptyStateView(
                icon: Icons.menu_book_rounded,
                title: 'Nothing on the menu yet.',
                description:
                    'Our catalog is currently being updated with exciting options. Please check back in a few minutes!',
                secondaryActionLabel: 'Refresh Menu',
                onSecondaryAction: () =>
                    ref.read(categoryListProvider.notifier).refresh(),
              );
            }

            return GridView.builder(
              padding: const EdgeInsets.all(20),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 2,
                mainAxisSpacing: 18,
                crossAxisSpacing: 18,
                childAspectRatio: 1.15,
              ),
              itemCount: categories.length,
              itemBuilder: (context, index) {
                final category = categories[index];
                return Material(
                  color: AppColors.surface,
                  borderRadius: AppRadius.lgBorder,
                  elevation: 0,
                  child: InkWell(
                    borderRadius: AppRadius.lgBorder,
                    onTap:
                        () => Navigator.of(context).push<void>(
                          MaterialPageRoute(
                            builder:
                                (_) => KioskItemListScreen(category: category),
                          ),
                        ),
                    child: Container(
                      decoration: BoxDecoration(
                        borderRadius: AppRadius.lgBorder,
                        border: Border.all(color: AppColors.border, width: 1.2),
                        boxShadow: AppShadows.subtle,
                      ),
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Container(
                            padding: const EdgeInsets.all(14),
                            decoration: BoxDecoration(
                              color: AppColors.brandPrimaryContainer,
                              shape: BoxShape.circle,
                            ),
                            child: const Icon(
                              Icons.fastfood_rounded,
                              size: 32,
                              color: AppColors.brandPrimary,
                            ),
                          ),
                          const SizedBox(height: 14),
                          Text(
                            category.name,
                            textAlign: TextAlign.center,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.w700,
                              color: AppColors.textPrimary,
                              letterSpacing: -0.2,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                );
              },
            );
          },
        ),
      ),
    );
  }
}
