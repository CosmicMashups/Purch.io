import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../../core/widgets/purch_image.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/domain/pricing_type.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../catalog/presentation/screens/add_item_screen.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import 'combo_customization_screen.dart';
import 'payment_screen.dart';
import 'variant_picker_screen.dart';

/// The Cashier screen — D1's item grid and cart, merged.
///
/// The shell is landscape-first, so on a tablet the grid is the primary
/// surface and the cart is a persistent side panel: a cashier sees the order
/// building up as they tap, instead of push/popping to a separate cart route
/// between every item. Below tablet width the cart moves into an end drawer
/// with a running total bar, so the same screen still works in portrait.
///
/// Both halves read and write the one [cartNotifierProvider], so there is no
/// state to hand between them. Shift, Promo Codes and X/Z Reading are
/// secondary: they sit as compact buttons in the app bar rather than taking a
/// full tile each.
class CashierScreen extends ConsumerStatefulWidget {
  const CashierScreen({super.key});

  @override
  ConsumerState<CashierScreen> createState() => _CashierScreenState();
}

/// The Cashier's lesser-used tools, in the order they sit in the app bar.
const _secondaryActions = <_SecondaryActionSpec>[
  _SecondaryActionSpec(
    icon: Icons.point_of_sale_outlined,
    label: 'Shift',
    path: '/cashier/shift',
  ),
  _SecondaryActionSpec(
    icon: Icons.local_offer_outlined,
    label: 'Promos',
    path: '/cashier/promo-codes',
  ),
  _SecondaryActionSpec(
    icon: Icons.receipt_long_outlined,
    label: 'X / Z',
    path: '/cashier/bir-reading',
  ),
];

class _SecondaryActionSpec {
  const _SecondaryActionSpec({
    required this.icon,
    required this.label,
    required this.path,
  });

  final IconData icon;
  final String label;
  final String path;
}

class _CashierScreenState extends ConsumerState<CashierScreen> {
  final _scaffoldKey = GlobalKey<ScaffoldState>();

  /// Width at which the cart earns a permanent column. Below this a side
  /// panel would squeeze the grid down to one item per row.
  static const _sidePanelBreakpoint = 720.0;
  static const _cartPanelWidth = 340.0;

  /// null means "All" — the grid shows every active item, as it always has.
  String? _selectedCategoryId;

  @override
  Widget build(BuildContext context) {
    final cart = ref.watch(cartNotifierProvider).valueOrNull;
    final itemCount = cart?.itemCount ?? 0;

    return LayoutBuilder(
      builder: (context, constraints) {
        final showSidePanel = constraints.maxWidth >= _sidePanelBreakpoint;

        final itemGrid = ItemGridPanel(
          selectedCategoryId: _selectedCategoryId,
          onClearCategory: () => setState(() => _selectedCategoryId = null),
        );
        final categorySelector = CategorySelector(
          vertical: showSidePanel,
          selectedCategoryId: _selectedCategoryId,
          onSelect: (id) => setState(() => _selectedCategoryId = id),
        );

        return Scaffold(
          key: _scaffoldKey,
          backgroundColor: AppColors.background,
          appBar: AppBar(
            title: const Text('Cashier'),
            actions: [
              // Three labelled buttons plus the cart badge don't fit beside
              // the title on a phone, and an AppBar's actions row has no
              // overflow protection — so narrow widths get one menu instead.
              if (showSidePanel)
                for (final action in _secondaryActions)
                  _SecondaryAction(spec: action)
              else
                const _SecondaryActionsMenu(),
              const SizedBox(width: AppSpacing.sm),
              if (!showSidePanel)
                Padding(
                  padding: const EdgeInsets.only(right: AppSpacing.sm),
                  child: IconButton(
                    onPressed: () => _scaffoldKey.currentState?.openEndDrawer(),
                    icon: Badge(
                      label: Text(
                        '$itemCount',
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
                      backgroundColor: AppColors.accentWarm,
                      textColor: Colors.white,
                      isLabelVisible: itemCount > 0,
                      child: const Icon(Icons.shopping_cart_rounded),
                    ),
                    tooltip: 'View cart',
                  ),
                ),
            ],
          ),
          endDrawer:
              showSidePanel
                  ? null
                  : const Drawer(
                    width: _cartPanelWidth,
                    backgroundColor: AppColors.surface,
                    child: SafeArea(child: CartPanel()),
                  ),
          body:
              showSidePanel
                  ? Row(
                    children: [
                      categorySelector,
                      Expanded(child: itemGrid),
                      const SizedBox(
                        width: _cartPanelWidth,
                        child: DecoratedBox(
                          decoration: BoxDecoration(
                            color: AppColors.surface,
                            border: Border(
                              left: BorderSide(color: AppColors.border),
                            ),
                          ),
                          child: CartPanel(),
                        ),
                      ),
                    ],
                  )
                  : Column(
                    children: [categorySelector, Expanded(child: itemGrid)],
                  ),
          bottomNavigationBar:
              showSidePanel || cart == null || cart.lines.isEmpty
                  ? null
                  : _CartSummaryBar(
                    cart: cart,
                    onOpen: () => _scaffoldKey.currentState?.openEndDrawer(),
                  ),
        );
      },
    );
  }
}

/// Compact app-bar entry point for the Cashier tab's lesser-used tools.
class _SecondaryAction extends StatelessWidget {
  const _SecondaryAction({required this.spec});

  final _SecondaryActionSpec spec;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 2),
      child: TextButton.icon(
        onPressed: () => context.push(spec.path),
        icon: Icon(spec.icon, size: 18),
        label: Text(spec.label),
        style: TextButton.styleFrom(
          foregroundColor: AppColors.textSecondary,
          visualDensity: VisualDensity.compact,
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.sm),
        ),
      ),
    );
  }
}

/// The same three tools, collapsed into one menu for narrow widths.
class _SecondaryActionsMenu extends StatelessWidget {
  const _SecondaryActionsMenu();

  @override
  Widget build(BuildContext context) {
    return PopupMenuButton<String>(
      icon: const Icon(Icons.more_vert_rounded),
      tooltip: 'More cashier tools',
      color: AppColors.surface,
      shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
      onSelected: (path) => context.push(path),
      itemBuilder:
          (context) => [
            for (final action in _secondaryActions)
              PopupMenuItem<String>(
                value: action.path,
                child: Row(
                  children: [
                    Icon(action.icon, size: 18, color: AppColors.textSecondary),
                    const SizedBox(width: AppSpacing.md),
                    Text(
                      action.label,
                      style: AppTypography.labelMd,
                    ),
                  ],
                ),
              ),
          ],
    );
  }
}

// ---------------------------------------------------------------------------
// Category selector
// ---------------------------------------------------------------------------

/// The inline category filter for the item grid: a vertical rail on the side
/// opposite the cart when there's room, a horizontal strip above the grid
/// when there isn't.
///
/// It is a filter, not a navigation step — the kiosk pushes a route per
/// category because a customer browses, while a cashier is mid-sale and the
/// grid has to stay on screen. If categories can't be loaded the selector
/// simply isn't drawn: a catalog-taxonomy problem must never stop a sale.
class CategorySelector extends ConsumerWidget {
  const CategorySelector({
    super.key,
    required this.vertical,
    required this.selectedCategoryId,
    required this.onSelect,
  });

  static const double railWidth = 104;
  static const double stripHeight = 100;

  final bool vertical;
  final String? selectedCategoryId;
  final ValueChanged<String?> onSelect;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final categories = ref.watch(categoryListProvider).valueOrNull;
    if (categories == null || categories.isEmpty) {
      return const SizedBox.shrink();
    }

    final sorted = [...categories]
      ..sort((a, b) => a.sortOrder.compareTo(b.sortOrder));

    final entries = <Widget>[
      _CategoryTile(
        label: 'All',
        icon: Icons.grid_view_rounded,
        imageUrl: null,
        vertical: vertical,
        selected: selectedCategoryId == null,
        onTap: () => onSelect(null),
      ),
      for (final category in sorted)
        _CategoryTile(
          label: category.name,
          icon: Icons.category_rounded,
          imageUrl: category.imageUrl,
          vertical: vertical,
          selected: selectedCategoryId == category.id,
          onTap: () => onSelect(category.id),
        ),
    ];

    if (vertical) {
      return Container(
        width: railWidth,
        decoration: const BoxDecoration(
          color: AppColors.surface,
          border: Border(right: BorderSide(color: AppColors.border)),
        ),
        child: ListView.separated(
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.sm,
            vertical: AppSpacing.md,
          ),
          itemCount: entries.length,
          separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
          itemBuilder: (context, index) => entries[index],
        ),
      );
    }

    return Container(
      height: stripHeight,
      decoration: const BoxDecoration(
        color: AppColors.surface,
        border: Border(bottom: BorderSide(color: AppColors.border)),
      ),
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.md,
          vertical: AppSpacing.sm,
        ),
        itemCount: entries.length,
        separatorBuilder: (_, _) => const SizedBox(width: AppSpacing.sm),
        itemBuilder: (context, index) => entries[index],
      ),
    );
  }
}

class _CategoryTile extends StatelessWidget {
  const _CategoryTile({
    required this.label,
    required this.icon,
    required this.imageUrl,
    required this.vertical,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final String? imageUrl;
  final bool vertical;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    const thumbSize = 34.0;

    final thumbnail = Container(
      width: thumbSize,
      height: thumbSize,
      decoration: BoxDecoration(
        color:
            selected ? AppColors.brandPrimaryContainer : AppColors.borderSubtle,
        borderRadius: BorderRadius.circular(AppRadius.sm),
      ),
      clipBehavior: Clip.antiAlias,
      child:
          imageUrl != null && imageUrl!.isNotEmpty
              ? PurchImage(
                imageUrlOrPath: imageUrl,
                width: thumbSize,
                height: thumbSize,
                borderRadius: BorderRadius.circular(AppRadius.sm),
                fit: BoxFit.cover,
              )
              : Icon(
                icon,
                size: 18,
                color:
                    selected ? AppColors.brandPrimary : AppColors.textSecondary,
              ),
    );

    final text = Text(
      label,
      textAlign: TextAlign.center,
      maxLines: 2,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(
        fontSize: 11,
        height: 1.15,
        fontWeight: selected ? FontWeight.w700 : FontWeight.w600,
        color: selected ? AppColors.brandPrimary : AppColors.textSecondary,
      ),
    );

    return Semantics(
      selected: selected,
      button: true,
      child: Material(
        color:
            selected ? AppColors.brandPrimaryContainer : Colors.transparent,
        borderRadius: AppRadius.mdBorder,
        child: InkWell(
          borderRadius: AppRadius.mdBorder,
          onTap: onTap,
          child: Container(
            width: vertical ? null : 78,
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.xs,
              vertical: AppSpacing.sm,
            ),
            decoration: BoxDecoration(
              borderRadius: AppRadius.mdBorder,
              border: Border.all(
                color: selected ? AppColors.brandPrimary : AppColors.border,
              ),
            ),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              mainAxisSize: MainAxisSize.min,
              children: [
                thumbnail,
                const SizedBox(height: AppSpacing.xs),
                Flexible(child: text),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _CartSummaryBar extends StatelessWidget {
  const _CartSummaryBar({required this.cart, required this.onOpen});

  final Transaction cart;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: AppSpacing.md,
        ),
        decoration: const BoxDecoration(
          color: AppColors.surface,
          boxShadow: AppShadows.card,
          border: Border(top: BorderSide(color: AppColors.border)),
        ),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    '${cart.itemCount} item${cart.itemCount == 1 ? '' : 's'} in cart',
                    style: AppTypography.bodySm,
                  ),
                  Text(
                    '₱${cart.totalAmount.toStringAsFixed(2)}',
                    style: AppTypography.priceLine.copyWith(fontSize: 18),
                  ),
                ],
              ),
            ),
            FilledButton.icon(
              onPressed: onOpen,
              icon: const Icon(Icons.shopping_cart_rounded, size: 18),
              label: const Text('Review cart'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.brandPrimary,
                foregroundColor: AppColors.onBrandPrimary,
                shape: const RoundedRectangleBorder(
                  borderRadius: AppRadius.mdBorder,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Item grid
// ---------------------------------------------------------------------------

/// D1's POS item grid, as a panel rather than a route. PricingType.unit items
/// are addable directly; PricingType.combo (D2) and PricingType.variantMatrix
/// (D3) open their own customization sheet.
class ItemGridPanel extends ConsumerWidget {
  const ItemGridPanel({
    super.key,
    this.selectedCategoryId,
    this.onClearCategory,
  });

  /// null means no category filter — every active item shows.
  final String? selectedCategoryId;

  /// Way back out of an empty category, offered in that empty state.
  final VoidCallback? onClearCategory;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final itemsAsync = ref.watch(itemListProvider);

    return itemsAsync.when(
      loading:
          () => const Center(
            child: CircularProgressIndicator(color: AppColors.brandPrimary),
          ),
      error:
          (error, stackTrace) => ErrorStateView(
            message: 'Could not load items: ${describeError(error)}',
            onRetry: () => ref.read(itemListProvider.notifier).refresh(),
          ),
      data: (items) {
        final activeItems = items.where((item) => item.isActive).toList();
        if (activeItems.isEmpty) {
          return EmptyStateView(
            icon: Icons.inventory_2_outlined,
            title: 'No active items to sell yet.',
            description:
                'Items will appear here for checkout once they are created and set to active in the catalog.',
            actionLabel: 'Add Items',
            onAction:
                () => Navigator.of(context).push<void>(
                  MaterialPageRoute(builder: (_) => const AddItemScreen()),
                ),
          );
        }

        final visibleItems =
            selectedCategoryId == null
                ? activeItems
                : activeItems
                    .where((item) => item.categoryId == selectedCategoryId)
                    .toList();

        // Distinct from an empty catalog: there *are* things to sell, just
        // not under this filter, so the way out is back to everything.
        if (visibleItems.isEmpty) {
          return EmptyStateView(
            icon: Icons.category_outlined,
            title: 'Nothing in this category yet.',
            description:
                'No active item is filed under this category. Assign items to '
                'it in the catalog, or keep selling from the full list.',
            actionLabel: 'Show All Items',
            onAction: onClearCategory,
          );
        }

        return LayoutBuilder(
          builder: (context, constraints) {
            // Keep tiles around 180dp wide whatever the grid's share of the
            // screen is, so the cart panel taking 340dp doesn't squash them.
            final columns = (constraints.maxWidth / 180).floor().clamp(2, 6);
            final tileWidth =
                (constraints.maxWidth -
                    AppSpacing.lg * 2 -
                    14 * (columns - 1)) /
                columns;
            // Height follows the 1.15 aspect the grid has always had, but
            // with a floor: with the category rail and the cart both taking
            // their share, a tile can end up narrow enough that a square-ish
            // box no longer holds icon + two-line name + price.
            final tileHeight = (tileWidth / 1.15).clamp(148.0, 200.0);

            return GridView.builder(
              padding: const EdgeInsets.all(AppSpacing.lg),
              gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: columns,
                mainAxisSpacing: 14,
                crossAxisSpacing: 14,
                mainAxisExtent: tileHeight,
              ),
              itemCount: visibleItems.length,
              itemBuilder:
                  (context, index) => _ItemTile(item: visibleItems[index]),
            );
          },
        );
      },
    );
  }
}

class _ItemTile extends ConsumerWidget {
  const _ItemTile({required this.item});

  final Item item;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isDirectlySellable = item.pricingType == PricingType.unit;
    final needsCustomization =
        item.pricingType == PricingType.combo ||
        item.pricingType == PricingType.variantMatrix;
    final badge = _ItemBadge.forItem(item);

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        boxShadow: AppShadows.subtle,
        border: Border.all(color: AppColors.border),
      ),
      clipBehavior: Clip.antiAlias,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: () async {
            if (item.pricingType == PricingType.variantMatrix) {
              await Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => VariantPickerScreen(item: item),
                ),
              );
              return;
            }

            if (item.pricingType == PricingType.combo) {
              await Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => ComboCustomizationScreen(item: item),
                ),
              );
              return;
            }

            if (!isDirectlySellable) {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  content: Text(
                    '${item.name} needs a customization step that '
                    'isn\'t built yet — only regular unit-priced '
                    'items can be added directly for now.',
                  ),
                ),
              );
              return;
            }

            final controller = ref.read(cartNotifierProvider.notifier);
            final succeeded = await controller.addLine(
              AddTransactionLineRequest(itemId: item.id, quantity: 1),
            );
            if (succeeded && context.mounted) {
              ScaffoldMessenger.of(
                context,
              ).showSnackBar(SnackBar(content: Text('Added ${item.name}')));
            }
          },
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Container(
                  width: 44,
                  height: 44,
                  decoration: BoxDecoration(
                    color:
                        isDirectlySellable || needsCustomization
                            ? AppColors.brandPrimaryContainer
                            : AppColors.cardHover,
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                  ),
                  child:
                      item.imageUrl != null && item.imageUrl!.isNotEmpty
                          ? PurchImage(
                            imageUrlOrPath: item.imageUrl,
                            width: 44,
                            height: 44,
                            borderRadius: BorderRadius.circular(AppRadius.sm),
                            fit: BoxFit.cover,
                          )
                          : Icon(
                            isDirectlySellable || needsCustomization
                                ? Icons.inventory_2_rounded
                                : Icons.inventory_2_outlined,
                            size: 22,
                            color:
                                isDirectlySellable || needsCustomization
                                    ? AppColors.brandPrimary
                                    : AppColors.textMuted,
                          ),
                ),
                const SizedBox(height: 10),
                Flexible(
                  child: Text(
                    item.name,
                    textAlign: TextAlign.center,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textPrimary,
                      height: 1.2,
                    ),
                  ),
                ),
                const SizedBox(height: 4),
                // Price and its qualifier share one line: the grid's tiles
                // are sized for speed, and a second line of type would cost
                // a row of items on a phone.
                FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        '₱${item.basePrice.toStringAsFixed(2)}',
                        style: AppTypography.priceBadge,
                      ),
                      if (badge != null) ...[
                        const SizedBox(width: 6),
                        _ItemTypeBadge(badge: badge),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// What a tile says about an item beyond its price.
///
/// The kiosk labels every item with its pricing type; the Cashier only
/// labels the ones that change what tapping the tile does, so the badge
/// stays a signal rather than noise on a wall of tiles. There is no unit
/// field on [Item], so a weight/volume price gets no invented "/kg" suffix —
/// it gets told, honestly, that the price is per unit of weight.
class _ItemBadge {
  const _ItemBadge({
    required this.label,
    required this.color,
    required this.background,
  });

  final String label;
  final Color color;
  final Color background;

  static _ItemBadge? forItem(Item item) {
    if (item.stockOnHand <= 0 && item.pricingType == PricingType.unit) {
      return const _ItemBadge(
        label: 'OUT OF STOCK',
        color: AppColors.onErrorContainer,
        background: AppColors.errorContainer,
      );
    }
    if (item.lowStockThreshold != null &&
        item.lowStockThreshold! > 0 &&
        item.stockOnHand <= item.lowStockThreshold! &&
        item.pricingType == PricingType.unit) {
      return const _ItemBadge(
        label: 'LOW STOCK',
        color: AppColors.onWarningContainer,
        background: AppColors.warningContainer,
      );
    }
    return switch (item.pricingType) {
      PricingType.combo => const _ItemBadge(
        label: 'COMBO',
        color: AppColors.onAccentWarmContainer,
        background: AppColors.accentWarmContainer,
      ),
      PricingType.variantMatrix => const _ItemBadge(
        label: 'OPTIONS',
        color: AppColors.onBrandPrimaryContainer,
        background: AppColors.brandPrimaryContainer,
      ),
      PricingType.weightVolume => const _ItemBadge(
        label: 'BY WEIGHT',
        color: AppColors.onInfoContainer,
        background: AppColors.infoContainer,
      ),
      PricingType.bundle => const _ItemBadge(
        label: 'BUNDLE',
        color: AppColors.onAccentWarmContainer,
        background: AppColors.accentWarmContainer,
      ),
      PricingType.service => const _ItemBadge(
        label: 'SERVICE',
        color: AppColors.onInfoContainer,
        background: AppColors.infoContainer,
      ),
      _ => null,
    };
  }
}

class _ItemTypeBadge extends StatelessWidget {
  const _ItemTypeBadge({required this.badge});

  final _ItemBadge badge;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: badge.background,
        borderRadius: BorderRadius.circular(AppRadius.sm),
        border: Border.all(color: badge.color.withOpacity(0.25), width: 0.8),
      ),
      child: Text(
        badge.label,
        style: TextStyle(
          fontSize: 9,
          fontWeight: FontWeight.w700,
          letterSpacing: 0.4,
          height: 1.2,
          color: badge.color,
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Cart
// ---------------------------------------------------------------------------

/// D1's cart review as a panel — quantity adjustment and line removal, plus
/// the way into D5's payment method tabs. Also D4's Senior/PWD toggle and
/// promo code entry.
class CartPanel extends ConsumerStatefulWidget {
  const CartPanel({super.key});

  @override
  ConsumerState<CartPanel> createState() => _CartPanelState();
}

class _CartPanelState extends ConsumerState<CartPanel> {
  final _promoCodeController = TextEditingController();

  @override
  void dispose() {
    _promoCodeController.dispose();
    super.dispose();
  }

  Future<void> _confirmVoid(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder:
          (context) => AlertDialog(
            shape: const RoundedRectangleBorder(
              borderRadius: AppRadius.lgBorder,
            ),
            title: const Text('Void cart?'),
            content: const Text(
              'This clears every item in the current sale. This cannot be undone.',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(false),
                child: const Text('Cancel'),
              ),
              FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.error,
                  foregroundColor: Colors.white,
                ),
                onPressed: () => Navigator.of(context).pop(true),
                child: const Text('Void'),
              ),
            ],
          ),
    );

    if (confirmed == true) {
      await ref.read(cartNotifierProvider.notifier).voidCart();
    }
  }

  @override
  Widget build(BuildContext context) {
    final cartAsync = ref.watch(cartNotifierProvider);
    final failure = ref.read(cartNotifierProvider.notifier).currentFailure;

    return Column(
      children: [
        Container(
          padding: const EdgeInsets.fromLTRB(
            AppSpacing.lg,
            AppSpacing.md,
            AppSpacing.sm,
            AppSpacing.md,
          ),
          decoration: const BoxDecoration(
            border: Border(bottom: BorderSide(color: AppColors.border)),
          ),
          child: Row(
            children: [
              Text('Cart', style: AppTypography.titleMd),
              const Spacer(),
              IconButton(
                onPressed:
                    cartAsync.valueOrNull == null
                        ? null
                        : () => _confirmVoid(context, ref),
                icon: const Icon(Icons.delete_sweep),
                tooltip: 'Void cart',
              ),
            ],
          ),
        ),
        Expanded(
          child: cartAsync.when(
            loading:
                () => const Center(
                  child: CircularProgressIndicator(
                    color: AppColors.brandPrimary,
                  ),
                ),
            error:
                (error, stackTrace) => ErrorStateView(
                  message: 'Could not load the cart: ${describeError(error)}',
                  onRetry: () => ref.invalidate(cartNotifierProvider),
                ),
            data: (cart) {
              if (cart.lines.isEmpty) {
                return const EmptyStateView(
                  icon: Icons.shopping_basket_outlined,
                  title: 'Cart is empty — tap an item to start the order.',
                  description:
                      'Items you tap in the grid are added here, ready to pay.',
                );
              }

              return Column(
                children: [
                  if (failure != null)
                    Padding(
                      padding: const EdgeInsets.all(12),
                      child: Container(
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: AppColors.error.withAlpha(20),
                          borderRadius: AppRadius.smBorder,
                          border: Border.all(color: AppColors.error.withAlpha(60)),
                        ),
                        child: Text(
                          failure.message,
                          style: const TextStyle(
                            color: AppColors.error,
                            fontSize: 13,
                            fontWeight: FontWeight.w500,
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ),
                    ),
                  Expanded(
                    child: ListView.separated(
                      padding: const EdgeInsets.symmetric(
                        horizontal: AppSpacing.md,
                        vertical: AppSpacing.md,
                      ),
                      itemCount: cart.lines.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 8),
                      itemBuilder:
                          (context, index) =>
                              _CartLineTile(line: cart.lines[index]),
                    ),
                  ),
                  _CartFooter(
                    cart: cart,
                    promoCodeController: _promoCodeController,
                  ),
                ],
              );
            },
          ),
        ),
      ],
    );
  }
}

class _CartFooter extends ConsumerWidget {
  const _CartFooter({required this.cart, required this.promoCodeController});

  final Transaction cart;
  final TextEditingController promoCodeController;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Container(
      decoration: const BoxDecoration(
        color: AppColors.surface,
        boxShadow: AppShadows.card,
        border: Border(top: BorderSide(color: AppColors.border)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          SwitchListTile(
            value: cart.seniorPwdDiscountApplied,
            activeColor: AppColors.brandPrimary,
            dense: true,
            onChanged:
                (value) => ref
                    .read(cartNotifierProvider.notifier)
                    .applySeniorPwdDiscount(value),
            title: const Text(
              'Senior Citizen/PWD Discount (20%)',
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
              ),
            ),
            subtitle: const Text(
              'Only apply after verifying the customer\'s physical ID.',
              style: TextStyle(fontSize: 11, color: AppColors.textSecondary),
            ),
          ),
          const Divider(color: AppColors.border, height: 1),
          Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: 10,
            ),
            child:
                cart.promoCode == null
                    ? Row(
                      children: [
                        Expanded(
                          child: TextField(
                            controller: promoCodeController,
                            decoration: const InputDecoration(
                              labelText: 'Promo code',
                              isDense: true,
                              prefixIcon: Icon(
                                Icons.discount_outlined,
                                size: 18,
                              ),
                            ),
                            textCapitalization: TextCapitalization.characters,
                          ),
                        ),
                        const SizedBox(width: 10),
                        FilledButton(
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            foregroundColor: AppColors.onBrandPrimary,
                          ),
                          onPressed:
                              () => ref
                                  .read(cartNotifierProvider.notifier)
                                  .applyPromoCode(
                                    promoCodeController.text.trim(),
                                  ),
                          child: const Text('Apply'),
                        ),
                      ],
                    )
                    : Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 6,
                      ),
                      decoration: const BoxDecoration(
                        color: AppColors.accentEmeraldContainer,
                        borderRadius: AppRadius.smBorder,
                      ),
                      child: Row(
                        children: [
                          const Icon(
                            Icons.check_circle_rounded,
                            size: 18,
                            color: AppColors.accentEmerald,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              'Promo code "${cart.promoCode}" applied',
                              style: const TextStyle(
                                color: AppColors.onAccentEmeraldContainer,
                                fontWeight: FontWeight.w600,
                                fontSize: 13,
                              ),
                            ),
                          ),
                          TextButton(
                            onPressed:
                                () => ref
                                    .read(cartNotifierProvider.notifier)
                                    .applyPromoCode(null),
                            child: const Text(
                              'Remove',
                              style: TextStyle(
                                color: AppColors.error,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
          ),
          const Divider(color: AppColors.border, height: 1),
          Padding(
            padding: const EdgeInsets.fromLTRB(
              AppSpacing.lg,
              AppSpacing.md,
              AppSpacing.lg,
              AppSpacing.lg,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _TotalsRow(label: 'Subtotal', amount: cart.subtotal),
                if (cart.discountAmount > 0)
                  _TotalsRow(label: 'Discount', amount: -cart.discountAmount),
                const SizedBox(height: 6),
                _TotalsRow(
                  label: 'Total',
                  amount: cart.totalAmount,
                  emphasize: true,
                ),
                const SizedBox(height: 14),
                SizedBox(
                  height: 52,
                  child: FilledButton(
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.brandPrimary,
                      foregroundColor: AppColors.onBrandPrimary,
                      shape: const RoundedRectangleBorder(
                        borderRadius: AppRadius.mdBorder,
                      ),
                    ),
                    onPressed:
                        () => Navigator.of(context, rootNavigator: true)
                            .push<void>(
                              MaterialPageRoute(
                                builder:
                                    (_) =>
                                        PaymentScreen(total: cart.totalAmount),
                              ),
                            ),
                    child: const Text(
                      'Pay',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _CartLineTile extends ConsumerWidget {
  const _CartLineTile({required this.line});

  final TransactionLine line;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final controller = ref.read(cartNotifierProvider.notifier);

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      line.itemName,
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    Text(
                      '₱${line.unitPrice.toStringAsFixed(2)} each',
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              Text(
                '₱${line.lineTotal.toStringAsFixed(2)}',
                style: AppTypography.priceLine,
              ),
            ],
          ),
          Row(
            children: [
              IconButton(
                onPressed: () => controller.removeLine(line.id),
                icon: const Icon(
                  Icons.delete_outline,
                  color: AppColors.error,
                  size: 20,
                ),
                visualDensity: VisualDensity.compact,
                tooltip: 'Remove',
              ),
              const Spacer(),
              IconButton(
                onPressed:
                    line.quantity > 1
                        ? () => controller.updateLine(
                          line.id,
                          UpdateTransactionLineRequest(
                            quantity: line.quantity - 1,
                          ),
                        )
                        : null,
                visualDensity: VisualDensity.compact,
                icon: const Icon(Icons.remove_circle_outline, size: 20),
              ),
              SizedBox(
                width: 32,
                child: Text(
                  line.quantity.toStringAsFixed(
                    line.quantity.truncateToDouble() == line.quantity ? 0 : 2,
                  ),
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 14,
                  ),
                ),
              ),
              IconButton(
                onPressed:
                    () => controller.updateLine(
                      line.id,
                      UpdateTransactionLineRequest(
                        quantity: line.quantity + 1,
                      ),
                    ),
                visualDensity: VisualDensity.compact,
                icon: const Icon(Icons.add_circle_outline, size: 20),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _TotalsRow extends StatelessWidget {
  const _TotalsRow({
    required this.label,
    required this.amount,
    this.emphasize = false,
  });

  final String label;
  final double amount;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    final textStyle =
        emphasize
            ? const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: AppColors.textPrimary,
            )
            : const TextStyle(fontSize: 14, color: AppColors.textSecondary);

    final amountStyle =
        emphasize
            ? const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w800,
              color: AppColors.brandPrimary,
              fontFeatures: [FontFeature.tabularFigures()],
            )
            : TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color:
                  amount < 0 ? AppColors.accentEmerald : AppColors.textPrimary,
              fontFeatures: const [FontFeature.tabularFigures()],
            );

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: textStyle),
        Text(
          amount < 0
              ? '₱-${(-amount).toStringAsFixed(2)}'
              : '₱${amount.toStringAsFixed(2)}',
          style: amountStyle,
        ),
      ],
    );
  }
}
