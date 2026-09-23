import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/formatting/money.dart';
import '../../../../core/routing/auth_gate.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../../core/widgets/purch_image.dart';
import '../../../../core/hardware/hardware_providers.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/domain/pricing_type.dart';
import '../../../catalog/domain/tingi_mode.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../catalog/presentation/screens/add_item_screen.dart';
import '../../../onboarding/domain/onboarding_enums.dart';
import '../../../onboarding/presentation/providers/onboarding_providers.dart';
import '../../../onboarding/presentation/screens/hardware_settings_screen.dart';
import '../../domain/pricing_engine.dart' show PromoCodeNotApplied;
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import 'combo_customization_screen.dart';
import 'item_modifier_customization_dialog.dart';
import 'payment_screen.dart';
import 'tingi_weight_dialog.dart';
import 'variant_picker_screen.dart';
import '../../../catalog/presentation/widgets/stale_catalog_banner.dart';
import '../widgets/offline_sales_banner.dart';

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
  _SecondaryActionSpec(
    icon: Icons.storefront_outlined,
    label: 'Kiosk Orders',
    path: '/cashier/pending-kiosk-orders',
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
  final _searchController = TextEditingController();
  String _searchQuery = '';

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  /// Width at which the cart earns a permanent column. Below this a side
  /// panel would squeeze the grid down to one item per row.
  static const _sidePanelBreakpoint = 720.0;
  static const _cartPanelWidth = 340.0;

  /// null means "All" — the grid shows every active item, as it always has.
  String? _selectedCategoryId;

  @override
  Widget build(BuildContext context) {
    ref.listen(cartNotifierProvider, (_, next) {
      final cartValue = next.valueOrNull;
      if (cartValue != null) {
        ref.read(cfdServiceProvider).updateFromTransaction(transaction: cartValue);
      }
    });

    final cart = ref.watch(cartNotifierProvider).valueOrNull;
    final itemCount = cart?.itemCount ?? 0;

    return LayoutBuilder(
      builder: (context, constraints) {
        final showSidePanel = constraints.maxWidth >= _sidePanelBreakpoint;

        final itemGrid = ItemGridPanel(
          selectedCategoryId: _selectedCategoryId,
          searchQuery: _searchQuery,
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
            centerTitle: false,
            titleSpacing: showSidePanel ? NavigationToolbar.kMiddleSpacing : 12.0,
            title: Row(
              children: [
                Container(
                  width: 32,
                  height: 32,
                  decoration: BoxDecoration(
                    color: AppColors.brandPrimary,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  alignment: Alignment.center,
                  child: const Text(
                    'P.',
                    style: TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w800,
                      fontSize: 14,
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                if (!showSidePanel || constraints.maxWidth < 960)
                  const Flexible(
                    child: Text(
                      'Cashier',
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontWeight: FontWeight.w800,
                        fontSize: 16,
                        letterSpacing: -0.3,
                      ),
                    ),
                  )
                else
                  Flexible(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Flexible(
                              child: Text(
                                'Cashier',
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(
                                  fontWeight: FontWeight.w800,
                                  fontSize: 16,
                                  letterSpacing: -0.3,
                                ),
                              ),
                            ),
                            const SizedBox(width: 8),
                            Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 7,
                                vertical: 2,
                              ),
                              decoration: BoxDecoration(
                                color: AppColors.brandPrimaryContainer,
                                borderRadius: BorderRadius.circular(AppRadius.full),
                                border: Border.all(
                                  color: AppColors.brandPrimary.withValues(alpha: 0.3),
                                ),
                              ),
                              child: const Text(
                                'Main Branch',
                                style: TextStyle(
                                  fontSize: 10,
                                  fontWeight: FontWeight.w700,
                                  color: AppColors.brandPrimary,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const Text(
                          'Register #01 • Online',
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 11,
                            color: AppColors.textSecondary,
                            fontWeight: FontWeight.normal,
                          ),
                        ),
                      ],
                    ),
                  ),
                if (showSidePanel && constraints.maxWidth >= 960) ...[
                  const SizedBox(width: 24),
                  Expanded(
                    child: Container(
                      height: 38,
                      constraints: const BoxConstraints(maxWidth: 380),
                      child: TextField(
                        controller: _searchController,
                        onChanged: (val) => setState(() => _searchQuery = val.trim()),
                        decoration: InputDecoration(
                          hintText: 'Scan barcode or search items... [F2]',
                          hintStyle: const TextStyle(fontSize: 12, color: AppColors.textMuted),
                          prefixIcon: const Icon(Icons.search_rounded, size: 18, color: AppColors.textSecondary),
                          suffixIcon: _searchQuery.isNotEmpty
                              ? IconButton(
                                  icon: const Icon(Icons.clear_rounded, size: 16),
                                  onPressed: () {
                                    _searchController.clear();
                                    setState(() => _searchQuery = '');
                                  },
                                )
                              : const Icon(Icons.qr_code_scanner_rounded, size: 18, color: AppColors.textSecondary),
                          contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 0),
                          fillColor: AppColors.card,
                          filled: true,
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(AppRadius.full),
                            borderSide: const BorderSide(color: AppColors.border),
                          ),
                          enabledBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(AppRadius.full),
                            borderSide: const BorderSide(color: AppColors.border),
                          ),
                          focusedBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(AppRadius.full),
                            borderSide: const BorderSide(color: AppColors.brandPrimary, width: 1.5),
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ],
            ),
            actions: [
              // Hardware actions and secondary buttons on wide screens;
              // collapsed into menu on narrow screens to prevent AppBar title overflow
              if (showSidePanel) ...[
                IconButton(
                  tooltip: 'Open Cash Drawer / No Sale',
                  icon: const Icon(Icons.point_of_sale_rounded, size: 20),
                  onPressed: () async {
                    try {
                      final success = await ref.read(cashDrawerServiceProvider).openManual(
                        operatorName: 'Cashier',
                        reason: 'Cashier Manual Open / No Sale',
                        isManagerOverride: true,
                      );
                      if (context.mounted) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(
                            content: Text(success ? 'Cash drawer opened.' : 'Failed to open drawer.'),
                            backgroundColor: success ? AppColors.accentEmerald : Colors.red,
                          ),
                        );
                      }
                    } catch (e) {
                      if (context.mounted) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(content: Text('$e'), backgroundColor: Colors.red),
                        );
                      }
                    }
                  },
                ),
                IconButton(
                  tooltip: 'Hardware Settings',
                  icon: const Icon(Icons.settings_input_component_rounded, size: 20),
                  onPressed: () => Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => const HardwareSettingsScreen()),
                  ),
                ),
                for (final action in _secondaryActions)
                  if (action.label != 'Kiosk Orders' || constraints.maxWidth >= 960)
                    _SecondaryAction(spec: action),
                if (constraints.maxWidth >= 1080) ...[
                  const SizedBox(width: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.brandPrimaryContainer,
                      borderRadius: BorderRadius.circular(AppRadius.full),
                      border: Border.all(color: AppColors.brandPrimary.withValues(alpha: 0.3)),
                    ),
                    child: const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        CircleAvatar(
                          radius: 11,
                          backgroundColor: AppColors.brandPrimary,
                          child: Text(
                            'MS',
                            style: TextStyle(fontSize: 10, fontWeight: FontWeight.bold, color: Colors.white),
                          ),
                        ),
                        SizedBox(width: 6),
                        Text(
                          'Maria S. • Cashier',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                            color: AppColors.brandPrimary,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
                const SizedBox(width: AppSpacing.sm),
              ] else ...[
                const _SecondaryActionsMenu(),
                const SizedBox(width: AppSpacing.sm),
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
          body: Column(
            children: [
              const StaleCatalogBanner(),
              const OfflineSalesBanner(),
              Expanded(
                child: _buildBody(showSidePanel, categorySelector, itemGrid),
              ),
            ],
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
  Widget _buildBody(
    bool showSidePanel,
    Widget categorySelector,
    Widget itemGrid,
  ) {
    return
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
        child: Column(
          children: [
            Container(
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.sm,
                AppSpacing.md,
                AppSpacing.sm,
                AppSpacing.xs,
              ),
              alignment: Alignment.center,
              child: const Text(
                'CATEGORIES',
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.w800,
                  letterSpacing: 1.0,
                  color: AppColors.textSecondary,
                ),
              ),
            ),
            Expanded(
              child: ListView.separated(
                padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.sm,
                  vertical: AppSpacing.xs,
                ),
                itemCount: entries.length,
                separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
                itemBuilder: (context, index) => entries[index],
              ),
            ),
          ],
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
    const thumbSize = 36.0;

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
                size: 20,
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
        fontWeight: selected ? FontWeight.w800 : FontWeight.w600,
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
            width: vertical ? null : 80,
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.xs,
              vertical: AppSpacing.sm + 2,
            ),
            decoration: BoxDecoration(
              borderRadius: AppRadius.mdBorder,
              border: Border.all(
                color: selected ? AppColors.brandPrimary : AppColors.border,
                width: selected ? 1.5 : 1,
              ),
              boxShadow:
                  selected
                      ? [
                        BoxShadow(
                          color: AppColors.brandPrimary.withValues(alpha: 0.12),
                          blurRadius: 6,
                          offset: const Offset(0, 2),
                        ),
                      ]
                      : null,
            ),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              mainAxisSize: MainAxisSize.min,
              children: [
                thumbnail,
                const SizedBox(height: 6),
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
                    formatCurrency(cart.totalAmount),
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
    this.searchQuery = '',
    this.onClearCategory,
  });

  /// null means no category filter — every active item shows.
  final String? selectedCategoryId;

  /// live query from AppBar search input.
  final String searchQuery;

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

        final categoryFiltered =
            selectedCategoryId == null
                ? activeItems
                : activeItems
                    .where((item) => item.categoryId == selectedCategoryId)
                    .toList();

        // Distinct from an empty catalog: there *are* things to sell, just
        // not under this filter, so the way out is back to everything.
        if (categoryFiltered.isEmpty) {
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

        final visibleItems =
            searchQuery.isEmpty
                ? categoryFiltered
                : categoryFiltered.where((item) {
                  final q = searchQuery.toLowerCase();
                  final nameMatches = item.name.toLowerCase().contains(q);
                  final skuMatches =
                      item.sku?.toLowerCase().contains(q) ?? false;
                  final barcodeMatches =
                      item.barcode?.toLowerCase().contains(q) ?? false;
                  return nameMatches || skuMatches || barcodeMatches;
                }).toList();

        if (visibleItems.isEmpty) {
          return EmptyStateView(
            icon: Icons.search_off_rounded,
            title: 'No matching items found.',
            description: 'No items match "$searchQuery" in this category.',
            actionLabel: 'Show All Items',
            onAction: onClearCategory,
          );
        }

        return LayoutBuilder(
          builder: (context, constraints) {
            // Keep tiles around 180dp wide whatever the grid's share of the
            // screen is, so the cart panel taking 360dp doesn't squash them.
            final columns = (constraints.maxWidth / 168).floor().clamp(2, 6);
            final tileWidth =
                (constraints.maxWidth -
                    AppSpacing.lg * 2 -
                    14 * (columns - 1)) /
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
    final isWeighed =
        item.pricingType == PricingType.weightVolume ||
        item.tingiMode != TingiMode.none;
    final isDirectlySellable =
        item.pricingType == PricingType.unit ||
        item.pricingType == PricingType.service ||
        item.pricingType == PricingType.bundle;
    final needsCustomization =
        item.pricingType == PricingType.combo ||
        item.pricingType == PricingType.variantMatrix;
    final badge = _ItemBadge.forItem(item);
    final isOutOfStock = badge?.label == 'OUT OF STOCK';

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(14),
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

            if (isWeighed) {
              await showDialog<bool>(
                context: context,
                builder: (_) => TingiWeightDialog(item: item),
              );
              return;
            }

            if (!isDirectlySellable) {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  content: Text(
                    '${item.name} needs a customization step for weight/portion selection.',
                  ),
                ),
              );
              return;
            }

            // Check if item has attached modifier groups
            final modifierGroups =
                await ref.read(itemModifierGroupListProvider(item.id).future);
            if (modifierGroups.isNotEmpty && context.mounted) {
              final added = await showDialog<bool>(
                context: context,
                builder: (_) => ItemModifierCustomizationDialog(item: item),
              );
              if (added == true && context.mounted) {
                ScaffoldMessenger.of(
                  context,
                ).showSnackBar(SnackBar(content: Text('Added ${item.name}')));
              }
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
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // --- Mockup Top Image Banner & Badge Overlay ---
              Expanded(
                child: Stack(
                  fit: StackFit.expand,
                  children: [
                    Container(
                      color: AppColors.cardHover,
                      child: item.imageUrl != null && item.imageUrl!.isNotEmpty
                          ? PurchImage(
                              imageUrlOrPath: item.imageUrl,
                              fit: BoxFit.cover,
                            )
                          : Center(
                              child: Container(
                                width: 44,
                                height: 44,
                                decoration: BoxDecoration(
                                  color: isDirectlySellable || needsCustomization || isWeighed
                                      ? AppColors.brandPrimaryContainer
                                      : AppColors.background,
                                  borderRadius: BorderRadius.circular(AppRadius.md),
                                ),
                                child: Icon(
                                  isDirectlySellable || needsCustomization || isWeighed
                                      ? (isWeighed ? Icons.scale_rounded : Icons.inventory_2_rounded)
                                      : Icons.inventory_2_outlined,
                                  size: 24,
                                  color: isDirectlySellable || needsCustomization || isWeighed
                                      ? AppColors.brandPrimary
                                      : AppColors.textMuted,
                                ),
                              ),
                            ),
                    ),
                    if (badge != null && !isOutOfStock)
                      Positioned(
                        top: 8,
                        left: 8,
                        child: _ItemTypeBadge(badge: badge),
                      ),
                    if (isOutOfStock)
                      Positioned.fill(
                        child: Container(
                          color: Colors.black.withValues(alpha: 0.45),
                          alignment: Alignment.center,
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 8,
                              vertical: 3,
                            ),
                            decoration: BoxDecoration(
                              color: AppColors.error,
                              borderRadius: BorderRadius.circular(AppRadius.full),
                            ),
                            child: const Text(
                              'OUT OF STOCK',
                              style: TextStyle(
                                fontSize: 9,
                                fontWeight: FontWeight.w800,
                                letterSpacing: 0.5,
                                color: Colors.white,
                              ),
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),

              // --- Mockup Bottom Content Area ---
              Padding(
                padding: const EdgeInsets.fromLTRB(10, 8, 10, 8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      item.name,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      crossAxisAlignment: CrossAxisAlignment.center,
                      children: [
                        Text(
                          formatCurrency(item.basePrice),
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w800,
                            color: AppColors.textPrimary,
                            fontFeatures: [FontFeature.tabularFigures()],
                          ),
                        ),
                        Container(
                          width: 22,
                          height: 22,
                          decoration: const BoxDecoration(
                            color: AppColors.brandPrimaryContainer,
                            shape: BoxShape.circle,
                          ),
                          child: const Icon(
                            Icons.add_rounded,
                            size: 16,
                            color: AppColors.brandPrimary,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
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
    // Authoritative: the server computes isOutOfStock from StockOnHand when the
    // tenant tracks stock directly, or from the recipe/InventoryItem system
    // when it doesn't — so this alone determines OOS, no separate stockOnHand
    // check here. (StockOnHand itself goes stale/unmaintained for tenants on
    // the recipe system, so re-checking it directly would show false
    // OUT OF STOCK badges once it drifts.) Kept scoped to unit-priced items,
    // matching pre-existing behavior, so combo/variant-matrix/etc. items don't
    // regress.
    if (item.isOutOfStock && item.pricingType == PricingType.unit) {
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
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: badge.background,
        borderRadius: BorderRadius.circular(AppRadius.full),
        border: Border.all(color: badge.color.withValues(alpha: 0.25), width: 0.8),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.1),
            blurRadius: 4,
            offset: const Offset(0, 1),
          ),
        ],
      ),
      child: Text(
        badge.label,
        style: TextStyle(
          fontSize: 9,
          fontWeight: FontWeight.w800,
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
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Row(
                      children: [
                        Text('Cart', style: AppTypography.titleMd),
                        const SizedBox(width: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 7,
                            vertical: 2,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.brandPrimaryContainer,
                            borderRadius: BorderRadius.circular(AppRadius.full),
                          ),
                          child: Text(
                            '${cartAsync.valueOrNull?.itemCount ?? 0} item${(cartAsync.valueOrNull?.itemCount ?? 0) == 1 ? '' : 's'}',
                            style: const TextStyle(
                              fontSize: 10,
                              fontWeight: FontWeight.w700,
                              color: AppColors.brandPrimary,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 1),
                    const Text(
                      'Ticket #0042 • Counter Sale',
                      style: TextStyle(
                        fontSize: 11,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              IconButton(
                onPressed:
                    cartAsync.valueOrNull == null
                        ? null
                        : () => _confirmVoid(context, ref),
                icon: const Icon(Icons.delete_sweep, color: AppColors.error),
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

              // The footer (Senior/PWD switch, promo code, totals, Pay) can get
              // tall — the discount explanations add text — so cap it and let it
              // scroll, rather than overflowing a short window.
              return LayoutBuilder(
                builder: (context, constraints) => Column(
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
                  ConstrainedBox(
                    constraints: BoxConstraints(
                      maxHeight:
                          constraints.hasBoundedHeight
                              ? constraints.maxHeight * 0.6
                              : double.infinity,
                    ),
                    child: SingleChildScrollView(
                      child: _CartFooter(
                        cart: cart,
                        promoCodeController: _promoCodeController,
                      ),
                    ),
                  ),
                ],
                ),
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
            // The backend only lets an Admin or Manager apply this discount
            // (an audit-trailed action), and re-checks it at checkout. Disable
            // it for everyone else rather than letting a cashier apply it on
            // screen and get the sale rejected at payment.
            onChanged:
                _canApplySeniorPwd(ref)
                    ? (value) => ref
                        .read(cartNotifierProvider.notifier)
                        .applySeniorPwdDiscount(value)
                    : null,
            title: const Text(
              'Senior Citizen/PWD Discount (20%)',
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
              ),
            ),
            subtitle: Text(
              _seniorPwdSubtitle(cart),
              style: const TextStyle(
                fontSize: 11,
                color: AppColors.textSecondary,
              ),
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
                      decoration: BoxDecoration(
                        color:
                            _promoCodeApplied(cart)
                                ? AppColors.accentEmeraldContainer
                                : AppColors.cardHover,
                        borderRadius: AppRadius.smBorder,
                      ),
                      child: Row(
                        children: [
                          Icon(
                            _promoCodeApplied(cart)
                                ? Icons.check_circle_rounded
                                : Icons.info_outline_rounded,
                            size: 18,
                            color:
                                _promoCodeApplied(cart)
                                    ? AppColors.accentEmerald
                                    : AppColors.textSecondary,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              _promoCodeMessage(cart),
                              style: TextStyle(
                                color:
                                    _promoCodeApplied(cart)
                                        ? AppColors.onAccentEmeraldContainer
                                        : AppColors.textSecondary,
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
          if (ref.watch(isDineInTakeOutVerticalProvider))
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.md,
                8,
                AppSpacing.md,
                4,
              ),
              child: SegmentedButton<String>(
                segments: const [
                  ButtonSegment(value: 'Dine In', label: Text('Dine In'), icon: Icon(Icons.restaurant_rounded)),
                  ButtonSegment(value: 'Take Out', label: Text('Take Out'), icon: Icon(Icons.takeout_dining_rounded)),
                ],
                selected: {if (cart.orderType != null) cart.orderType!},
                emptySelectionAllowed: true,
                onSelectionChanged: (selection) {
                  if (selection.isNotEmpty) {
                    ref.read(cartNotifierProvider.notifier).setOrderType(selection.first);
                  }
                },
              ),
            ),
          const Divider(color: AppColors.border, height: 1),
          if (MediaQuery.sizeOf(context).height >= 700) ...[
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.md,
                0,
                AppSpacing.md,
                8,
              ),
              child: Row(
                children: [
                  for (final tender in ['CASH', 'QR PH', 'CARD', 'CREDIT'])
                    Expanded(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 2),
                        child: InkWell(
                          borderRadius: BorderRadius.circular(AppRadius.sm),
                          onTap: () => _goToPayment(context, ref, cart),
                          child: Container(
                            padding: const EdgeInsets.symmetric(vertical: 5),
                            decoration: BoxDecoration(
                              color: AppColors.background,
                              borderRadius: BorderRadius.circular(AppRadius.sm),
                              border: Border.all(color: AppColors.border),
                            ),
                            alignment: Alignment.center,
                            child: Text(
                              tender,
                              style: const TextStyle(
                                fontSize: 10,
                                fontWeight: FontWeight.w700,
                                color: AppColors.textSecondary,
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ],
          Padding(
            padding: const EdgeInsets.fromLTRB(
              AppSpacing.md,
              AppSpacing.sm,
              AppSpacing.md,
              AppSpacing.md,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _TotalsRow(label: 'Subtotal', amount: cart.subtotal),
                if (cart.itemPromoDiscountAmount > 0)
                  _TotalsRow(
                    label: 'Item promos',
                    amount: -cart.itemPromoDiscountAmount,
                  ),
                if (cart.discountAmount > 0)
                  _TotalsRow(
                    label: _discountLabel(cart),
                    amount: -cart.discountAmount,
                  ),
                const SizedBox(height: 4),
                _TotalsRow(
                  label: 'Total',
                  amount: cart.totalAmount,
                  emphasize: true,
                ),
                const SizedBox(height: 10),
                SizedBox(
                  height: 48,
                  child: FilledButton(
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.brandPrimary,
                      foregroundColor: AppColors.onBrandPrimary,
                      shape: const RoundedRectangleBorder(
                        borderRadius: AppRadius.mdBorder,
                      ),
                      elevation: 1,
                    ),
                    onPressed: () => _goToPayment(context, ref, cart),
                    child: const Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.point_of_sale_rounded, size: 18),
                        SizedBox(width: 8),
                        Text(
                          'Pay',
                          style: TextStyle(
                            fontSize: 17,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        SizedBox(width: 8),
                        Icon(Icons.arrow_forward_rounded, size: 16),
                      ],
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

/// Guards checkout for Restaurant/Cafe tenants: Dine In/Take Out must be
/// chosen first, matching what the kiosk flow already requires before it
/// will submit an order.
void _goToPayment(BuildContext context, WidgetRef ref, Transaction cart) {
  final requiresOrderType = ref.read(isDineInTakeOutVerticalProvider);
  if (requiresOrderType && cart.orderType == null) {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Select Dine In or Take Out before checkout.')),
    );
    return;
  }

  Navigator.of(context, rootNavigator: true).push<void>(
    MaterialPageRoute(builder: (_) => PaymentScreen(total: cart.totalAmount)),
  );
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
        boxShadow: AppShadows.subtle,
      ),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
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
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    Text(
                      '${formatCurrency(line.unitPrice)} each',
                      style: const TextStyle(
                        fontSize: 11,
                        color: AppColors.textSecondary,
                      ),
                    ),
                    if (line.itemVariantAttributes.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Text(
                          line.variantAttributesLabel,
                          style: const TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w600,
                            color: AppColors.textSecondary,
                            fontStyle: FontStyle.italic,
                          ),
                        ),
                      ),
                    if (line.comboSelections.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 3),
                        child: Wrap(
                          spacing: 4,
                          runSpacing: 2,
                          children: [
                            for (final selection in line.comboSelections)
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 6,
                                  vertical: 1,
                                ),
                                decoration: BoxDecoration(
                                  color: AppColors.accentWarmContainer,
                                  borderRadius: BorderRadius.circular(4),
                                ),
                                child: Text(
                                  '${selection.slotLabel}: ${selection.selectedItemName}',
                                  style: const TextStyle(
                                    fontSize: 10,
                                    fontWeight: FontWeight.w600,
                                    color: AppColors.onAccentWarmContainer,
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                    if (line.modifierSelections.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 3),
                        child: Wrap(
                          spacing: 4,
                          runSpacing: 2,
                          children: [
                            for (final mod in line.modifierSelections)
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 6,
                                  vertical: 1,
                                ),
                                decoration: BoxDecoration(
                                  color: AppColors.brandPrimaryContainer,
                                  borderRadius: BorderRadius.circular(4),
                                ),
                                child: Text(
                                  mod.priceDelta > 0
                                      ? '+ ${mod.modifierName} (${formatCurrency(mod.priceDelta)})'
                                      : '+ ${mod.modifierName}',
                                  style: const TextStyle(
                                    fontSize: 10,
                                    fontWeight: FontWeight.w600,
                                    color: AppColors.brandPrimary,
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                    if (line.appliedPromoLabel != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 3),
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 6,
                            vertical: 1,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.accentEmeraldContainer,
                            borderRadius: BorderRadius.circular(4),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(
                                Icons.local_offer_outlined,
                                size: 11,
                                color: AppColors.accentEmerald,
                              ),
                              const SizedBox(width: 3),
                              Text(
                                line.appliedPromoLabel!,
                                style: const TextStyle(
                                  fontSize: 10,
                                  fontWeight: FontWeight.w700,
                                  color: AppColors.accentEmerald,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              Text(
                formatCurrency(line.lineTotal),
                style: AppTypography.priceLine.copyWith(
                  fontWeight: FontWeight.w800,
                  fontSize: 15,
                ),
              ),
            ],
          ),
          const SizedBox(height: 2),
          Row(
            children: [
              IconButton(
                onPressed: () => controller.removeLine(line.id),
                icon: const Icon(
                  Icons.delete_outline,
                  color: AppColors.error,
                  size: 20,
                ),
                constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
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
                constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
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
                    fontWeight: FontWeight.w800,
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
                constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
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
        // Flexible so a long label (e.g. "Promo code (SUMMER2026)") truncates
        // instead of overflowing the row on a narrow cart panel.
        Flexible(
          child: Text(
            label,
            style: textStyle,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ),
        const SizedBox(width: 8),
        Text(
          formatCurrency(amount),
          style: amountStyle,
        ),
      ],
    );
  }
}

String _peso(double amount) => formatCurrency(amount);

/// The line under the Senior/PWD switch. Senior/PWD and promotions never
/// combine, so it puts both amounts side by side for the cashier to let the
/// customer pick the better deal.
String _seniorPwdSubtitle(Transaction cart) {
  const idNote = 'Only apply after verifying the customer\'s physical ID.';
  final senior = cart.seniorPwdSavings;
  final promos = cart.promoSavings;
  if (senior == null || promos == null || (senior <= 0 && promos <= 0)) {
    return idNote;
  }
  final comparison =
      cart.seniorPwdDiscountApplied
          ? 'Applied instead of promotions (they would save ${_peso(promos)}).'
          : 'Would save ${_peso(senior)}; current promotions save ${_peso(promos)}. '
              'They can\'t be combined.';
  return '$idNote\n$comparison';
}

/// Whether the cart's promo code is actually discounting.
bool _promoCodeApplied(Transaction cart) =>
    cart.promoCodeNotApplied == PromoCodeNotApplied.none;

String _promoCodeMessage(Transaction cart) {
  final code = cart.promoCode;
  return switch (cart.promoCodeNotApplied) {
    PromoCodeNotApplied.none => 'Promo code "$code" applied',
    PromoCodeNotApplied.suppressedBySeniorPwd =>
      'Promo code "$code" not applied — it can\'t be combined with the '
          'Senior/PWD discount',
    PromoCodeNotApplied.supersededByItemPromos =>
      'Promo code "$code" not applied — the item promotions save more '
          '(only one promotion applies)',
  };
}

/// Names the one discount that applied, since Senior/PWD and promotions
/// never combine.
String _discountLabel(Transaction cart) {
  if (cart.seniorPwdDiscountApplied) {
    return 'Senior/PWD (20%)';
  }
  if (cart.promoCode != null && cart.promoDiscountAmount > 0) {
    return 'Promo code (${cart.promoCode})';
  }
  return 'Discount';
}

/// Whether the signed-in staff member may apply the Senior/PWD discount —
/// mirrors the backend's Admin/Manager-only rule.
bool _canApplySeniorPwd(WidgetRef ref) {
  final role = ref.watch(currentStaffRoleProvider).valueOrNull;
  return role == StaffRole.admin || role == StaffRole.manager;
}
