import type { BogoRule, ComboRule, ItemDiscountRule, PricingRules, PromoDiscountType } from '../pos/pricing/pricingEngine';

/** Mirrors Purch.Domain.Enums.PromoDiscountType, which the API sends as an integer. */
const DISCOUNT_TYPES: PromoDiscountType[] = ['percentage', 'fixedAmount', 'fixedPrice'];

type Wire<T> = Omit<T, 'discountType'> & { discountType?: number };

/** What `GET /kiosk/promo-rules` returns: the same rule shapes the Cashier's promotions screens use. */
export interface KioskPromoRulesResponse {
  bogo: BogoRule[];
  combos: ComboRule[];
  itemDiscounts: Wire<ItemDiscountRule>[];
}

export function toPricingRules(response: KioskPromoRulesResponse): PricingRules {
  return {
    bogo: response.bogo,
    combo: response.combos,
    itemDiscount: response.itemDiscounts.map((rule) => ({ ...rule, discountType: DISCOUNT_TYPES[rule.discountType ?? 0] })),
  };
}
