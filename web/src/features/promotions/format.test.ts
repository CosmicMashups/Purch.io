import { describe, expect, it } from 'vitest';
import { describeDiscount } from './format';
import { PromoDiscountType } from './types';

describe('describeDiscount', () => {
  it('describes each discount type', () => {
    expect(describeDiscount(PromoDiscountType.Percentage, 15)).toBe('15% off');
    expect(describeDiscount(PromoDiscountType.FixedAmount, 20)).toMatch(/₱20\.00 off/);
    expect(describeDiscount(PromoDiscountType.FixedPrice, 99)).toMatch(/Priced at ₱99\.00/);
  });
});
