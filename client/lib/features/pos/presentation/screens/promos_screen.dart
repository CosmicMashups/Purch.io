import 'package:flutter/material.dart';

import '../../../../core/theming/app_tokens.dart';
import 'bogo_promo_tab.dart';
import 'combo_promo_tab.dart';
import 'item_discount_promo_tab.dart';
import 'promo_code_list_screen.dart';

/// Admin/Manager promo management — the code-entry PromoCode plus the three
/// automatic (no code), item-targeted, time-boxed promo types (BOGO, Combo
/// bundle, Item discount) that apply themselves at checkout.
///
/// The first tab embeds [PromoCodeListView] (PromoCodeListScreen's body/FAB,
/// without its own AppBar) so there's a single app bar for the whole screen.
class PromosScreen extends StatefulWidget {
  const PromosScreen({super.key});

  @override
  State<PromosScreen> createState() => _PromosScreenState();
}

class _PromosScreenState extends State<PromosScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 4, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Promos'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        bottom: TabBar(
          controller: _tabController,
          isScrollable: true,
          labelColor: AppColors.brandPrimary,
          unselectedLabelColor: AppColors.textSecondary,
          indicatorColor: AppColors.brandPrimary,
          tabs: const [
            Tab(text: 'Promo Codes'),
            Tab(text: 'Buy 1 Take 1'),
            Tab(text: 'Combo Deals'),
            Tab(text: 'Item Discounts'),
          ],
        ),
      ),
      body: TabBarView(
        controller: _tabController,
        children: const [
          PromoCodeListView(),
          BogoPromoTab(),
          ComboPromoTab(),
          ItemDiscountPromoTab(),
        ],
      ),
    );
  }
}
