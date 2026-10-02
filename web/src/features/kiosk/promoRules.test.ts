import { describe, expect, it } from 'vitest';
import { toPricingRules } from './promoRules';

describe('toPricingRules', () => {
  it('turns the API integer discount types into the engine names and renames combos', () => {
    const rules = toPricingRules({
      bogo: [],
      combos: [{ itemAId: 'a', itemBId: 'b', comboPrice: 99, isActive: true }],
      itemDiscounts: [
        { itemId: 'a', discountValue: 10, discountType: 0 },
        { itemId: 'b', discountValue: 5, discountType: 1 },
        { itemId: 'c', discountValue: 80, discountType: 2 },
      ],
    });

    expect(rules.combo).toHaveLength(1);
    expect(rules.itemDiscount?.map((r) => r.discountType)).toEqual(['percentage', 'fixedAmount', 'fixedPrice']);
  });
});
