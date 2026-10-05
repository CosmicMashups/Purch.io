import { describe, expect, it } from 'vitest';
import { PricingType, type Item } from '../catalog/types';
import { MAX_QUANTITY, quantityCap } from './quantityCap';

const item = (over: Partial<Item> = {}) => ({ id: 'i', name: 'Latte', stockOnHand: 5, pricingType: PricingType.Unit, ...over }) as Item;

describe('quantityCap', () => {
  it('limits to the items own stock when the business counts stock on its items', () => {
    expect(quantityCap(item({ stockOnHand: 5 }), false, 0)).toEqual({ max: 5, limitedByStock: true });
  });

  it('takes away what the same item already has on other lines of the order', () => {
    expect(quantityCap(item({ stockOnHand: 5 }), false, 3)).toEqual({ max: 2, limitedByStock: true });
    expect(quantityCap(item({ stockOnHand: 5 }), false, 9)).toEqual({ max: 0, limitedByStock: true });
  });

  it('uses the general limit when there is plenty of stock', () => {
    expect(quantityCap(item({ stockOnHand: 500 }), false, 0)).toEqual({ max: MAX_QUANTITY, limitedByStock: false });
  });

  it('does not trust the items own count when stock is tracked through ingredients, or when that is not yet known', () => {
    expect(quantityCap(item({ stockOnHand: 0 }), true, 0).limitedByStock).toBe(false);
    expect(quantityCap(item({ stockOnHand: 0 }), undefined, 0).limitedByStock).toBe(false);
  });

  it('does not apply a count to combos, services or weighed items', () => {
    for (const pricingType of [PricingType.Combo, PricingType.Service, PricingType.WeightVolume]) {
      expect(quantityCap(item({ pricingType, stockOnHand: 0 }), false, 0).limitedByStock).toBe(false);
    }
  });
});
