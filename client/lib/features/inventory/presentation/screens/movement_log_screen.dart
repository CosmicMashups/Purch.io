import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../../core/widgets/load_more_footer.dart';
import '../../domain/inventory_movement_models.dart';
import '../providers/inventory_providers.dart';
import 'record_movement_screen.dart';

/// C2 — the stock movement log, filterable by movement type via the chip
/// row (matches PAGES.md's "movement type filter chips").
class MovementLogScreen extends ConsumerStatefulWidget {
  const MovementLogScreen({super.key});

  @override
  ConsumerState<MovementLogScreen> createState() => _MovementLogScreenState();
}

class _MovementLogScreenState extends ConsumerState<MovementLogScreen> {
  MovementType? _typeFilter;

  @override
  Widget build(BuildContext context) {
    final movementsAsync = ref.watch(movementLogProvider(type: _typeFilter));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Stock Movement Log')),
      body: Column(
        children: [
          Container(
            color: AppColors.surface,
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.sm,
            ),
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  ChoiceChip(
                    label: const Text('All'),
                    selected: _typeFilter == null,
                    selectedColor: AppColors.brandPrimaryContainer,
                    labelStyle: TextStyle(
                      fontWeight: FontWeight.w600,
                      color: _typeFilter == null
                          ? AppColors.brandPrimary
                          : AppColors.textSecondary,
                    ),
                    side: BorderSide(
                      color: _typeFilter == null
                          ? AppColors.brandPrimary
                          : AppColors.border,
                    ),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppRadius.full),
                    ),
                    onSelected: (_) => setState(() => _typeFilter = null),
                  ),
                  for (final type in MovementType.selectable) ...[
                    const SizedBox(width: AppSpacing.sm),
                    ChoiceChip(
                      label: Text(type.label),
                      selected: _typeFilter == type,
                      selectedColor: AppColors.brandPrimaryContainer,
                      labelStyle: TextStyle(
                        fontWeight: FontWeight.w600,
                        color: _typeFilter == type
                            ? AppColors.brandPrimary
                            : AppColors.textSecondary,
                      ),
                      side: BorderSide(
                        color: _typeFilter == type
                            ? AppColors.brandPrimary
                            : AppColors.border,
                      ),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(AppRadius.full),
                      ),
                      onSelected: (_) => setState(() => _typeFilter = type),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const Divider(height: 1, color: AppColors.borderSubtle),
          Expanded(
            child: movementsAsync.when(
              loading: () => const Center(
                child: CircularProgressIndicator(color: AppColors.brandPrimary),
              ),
              error:
                  (error, stackTrace) => ErrorStateView(
                    message: error.toString(),
                    onRetry:
                        () => ref.refresh(
                          movementLogProvider(type: _typeFilter),
                        ),
                  ),
              data: (movements) {
                if (movements.isEmpty) {
                  return EmptyStateView(
                    icon: Icons.history_toggle_off_rounded,
                    title: 'No movements recorded yet.',
                    description:
                        'Log received deliveries, damaged goods, waste, transfers, or cycle count adjustments.',
                    actionLabel: 'Record Stock Movement',
                    onAction:
                        () => Navigator.of(context).push<void>(
                          MaterialPageRoute(
                            builder: (_) => const RecordMovementScreen(),
                          ),
                        ),
                  );
                }

                final notifier = ref.read(
                  movementLogProvider(type: _typeFilter).notifier,
                );
                final showFooter = notifier.hasMore;

                return ListView.separated(
                  padding: const EdgeInsets.all(AppSpacing.md),
                  itemCount: movements.length + (showFooter ? 1 : 0),
                  separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
                  itemBuilder: (context, index) {
                    if (index == movements.length) {
                      return LoadMoreFooter(onLoadMore: notifier.loadMore);
                    }
                    final movement = movements[index];
                    final isIncrease =
                        movement.type == MovementType.stockIn ||
                        (movement.type == MovementType.adjustment &&
                            movement.quantity > 0);

                    return Container(
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: AppRadius.lgBorder,
                        border: Border.all(color: AppColors.border),
                        boxShadow: AppShadows.subtle,
                      ),
                      child: ListTile(
                        contentPadding: const EdgeInsets.symmetric(
                          horizontal: AppSpacing.md,
                          vertical: AppSpacing.xs,
                        ),
                        leading: Container(
                          width: 42,
                          height: 42,
                          decoration: BoxDecoration(
                            color: isIncrease
                                ? AppColors.accentEmeraldContainer
                                : AppColors.cardHover,
                            borderRadius: BorderRadius.circular(AppRadius.full),
                          ),
                          child: Icon(
                            isIncrease
                                ? Icons.arrow_upward_rounded
                                : Icons.arrow_downward_rounded,
                            color: isIncrease
                                ? AppColors.accentEmerald
                                : AppColors.error,
                            size: 20,
                          ),
                        ),
                        title: Text(
                          movement.itemName,
                          style: const TextStyle(
                            fontWeight: FontWeight.w700,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        subtitle: Text(
                          '${movement.type.label} · ${movement.branchName} · '
                          '${movement.staffUserName}'
                          '${movement.note != null ? ' · ${movement.note}' : ''}',
                          style: const TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 13,
                          ),
                        ),
                        trailing: Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: AppSpacing.sm,
                            vertical: 4,
                          ),
                          decoration: BoxDecoration(
                            color: isIncrease
                                ? AppColors.accentEmeraldContainer
                                : AppColors.cardHover,
                            borderRadius: BorderRadius.circular(AppRadius.full),
                          ),
                          child: Text(
                            movement.quantity.toStringAsFixed(
                              movement.quantity.truncateToDouble() ==
                                      movement.quantity
                                  ? 0
                                  : 2,
                            ),
                            style: TextStyle(
                              fontWeight: FontWeight.w800,
                              fontFeatures: const [FontFeature.tabularFigures()],
                              color: isIncrease
                                  ? AppColors.accentEmerald
                                  : AppColors.error,
                            ),
                          ),
                        ),
                      ),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const RecordMovementScreen()),
            ),
        tooltip: 'Record movement',
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: AppColors.onBrandPrimary,
        child: const Icon(Icons.add),
      ),
    );
  }
}
