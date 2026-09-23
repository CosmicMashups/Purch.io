import 'package:flutter/material.dart';

import '../theming/app_tokens.dart';

/// Gentle pulsing skeleton placeholder box matching Purch design system tokens.
class SkeletonBox extends StatefulWidget {
  const SkeletonBox({
    super.key,
    this.width,
    this.height,
    this.borderRadius,
    this.margin,
  });

  final double? width;
  final double? height;
  final BorderRadius? borderRadius;
  final EdgeInsetsGeometry? margin;

  @override
  State<SkeletonBox> createState() => _SkeletonBoxState();
}

class _SkeletonBoxState extends State<SkeletonBox>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _opacity;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1000),
    )..repeat(reverse: true);
    _opacity = Tween<double>(begin: 0.45, end: 0.9).animate(
      CurvedAnimation(parent: _controller, curve: Curves.easeInOut),
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FadeTransition(
      opacity: _opacity,
      child: Container(
        width: widget.width,
        height: widget.height,
        margin: widget.margin,
        decoration: BoxDecoration(
          color: AppColors.borderSubtle,
          borderRadius: widget.borderRadius ?? AppRadius.smBorder,
        ),
      ),
    );
  }
}

/// Skeleton grid of catalog item cards used by CashierScreen and KioskItemListScreen.
class CatalogGridSkeleton extends StatelessWidget {
  const CatalogGridSkeleton({
    super.key,
    this.itemCount = 8,
  });

  final int itemCount;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final columns = (constraints.maxWidth / 168).floor().clamp(2, 6);
        final tileWidth =
            (constraints.maxWidth - AppSpacing.lg * 2 - 14 * (columns - 1)) /
            columns;
        final tileHeight = (tileWidth * 1.12).clamp(168.0, 220.0);

        return GridView.builder(
          padding: const EdgeInsets.all(AppSpacing.lg),
          gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: columns,
            mainAxisSpacing: 14,
            crossAxisSpacing: 14,
            mainAxisExtent: tileHeight,
          ),
          itemCount: itemCount,
          itemBuilder: (context, index) => Container(
            decoration: BoxDecoration(
              color: AppColors.card,
              borderRadius: AppRadius.mdBorder,
              border: Border.all(color: AppColors.border),
            ),
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: SkeletonBox(
                    width: double.infinity,
                    borderRadius: AppRadius.smBorder,
                  ),
                ),
                const SizedBox(height: AppSpacing.sm),
                const SkeletonBox(width: 90, height: 14),
                const SizedBox(height: 6),
                const SkeletonBox(width: 55, height: 16),
              ],
            ),
          ),
        );
      },
    );
  }
}

/// Skeleton list of catalog or inventory items used by ItemListScreen.
class ItemListSkeleton extends StatelessWidget {
  const ItemListSkeleton({super.key, this.itemCount = 6});

  final int itemCount;

  @override
  Widget build(BuildContext context) {
    return ListView.separated(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      itemCount: itemCount,
      separatorBuilder: (_, __) => const SizedBox(height: 8),
      itemBuilder: (context, index) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: AppColors.card,
          borderRadius: AppRadius.mdBorder,
          border: Border.all(color: AppColors.border),
        ),
        child: Row(
          children: [
            const SkeletonBox(
              width: 44,
              height: 44,
              borderRadius: AppRadius.smBorder,
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: const [
                  SkeletonBox(width: 140, height: 15),
                  SizedBox(height: 6),
                  SkeletonBox(width: 90, height: 12),
                ],
              ),
            ),
            const SkeletonBox(width: 60, height: 18),
          ],
        ),
      ),
    );
  }
}

/// Tabular report skeleton for loading state of sales and performance tables.
class ReportTableSkeleton extends StatelessWidget {
  const ReportTableSkeleton({
    super.key,
    this.rowCount = 5,
    this.columnCount = 4,
  });

  final int rowCount;
  final int columnCount;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: AppRadius.lgBorder,
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: List.generate(
              columnCount,
              (i) => Expanded(
                child: SkeletonBox(
                  width: double.infinity,
                  height: 14,
                  margin: EdgeInsets.only(right: i < columnCount - 1 ? 12 : 0),
                ),
              ),
            ),
          ),
          const Divider(height: 24, color: AppColors.border),
          for (var r = 0; r < rowCount; r++) ...[
            if (r > 0) const SizedBox(height: 12),
            Row(
              children: List.generate(
                columnCount,
                (i) => Expanded(
                  child: SkeletonBox(
                    width: double.infinity,
                    height: 18,
                    margin: EdgeInsets.only(right: i < columnCount - 1 ? 12 : 0),
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
