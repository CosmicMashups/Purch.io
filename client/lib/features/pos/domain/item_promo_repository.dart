import 'item_promo_models.dart';

/// Admin/Manager-facing management for the three automatic, item-targeted,
/// time-boxed promo types (BOGO, Combo bundle, Item discount) — separate
/// from PromoCodeRepository, mirroring the backend's PromoCodeEndpoints
/// split into `/promos/bogo`, `/promos/combos`, `/promos/item-discounts`.
abstract class ItemPromoRepository {
  Future<List<BogoPromoRule>> listBogoPromoRules();

  Future<BogoPromoRule> createBogoPromoRule(CreateBogoPromoRuleRequest request);

  Future<BogoPromoRule> updateBogoPromoRule(
    String id,
    UpdateBogoPromoRuleRequest request,
  );

  Future<List<ComboPromoRule>> listComboPromoRules();

  Future<ComboPromoRule> createComboPromoRule(
    CreateComboPromoRuleRequest request,
  );

  Future<ComboPromoRule> updateComboPromoRule(
    String id,
    UpdateComboPromoRuleRequest request,
  );

  Future<List<ItemDiscountPromoRule>> listItemDiscountPromoRules();

  Future<ItemDiscountPromoRule> createItemDiscountPromoRule(
    CreateItemDiscountPromoRuleRequest request,
  );

  Future<ItemDiscountPromoRule> updateItemDiscountPromoRule(
    String id,
    UpdateItemDiscountPromoRuleRequest request,
  );
}
