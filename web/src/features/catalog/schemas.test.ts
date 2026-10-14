import { describe, expect, it } from 'vitest';
import { createItemSchema, parseCostPrice, updateItemSchema } from './schemas';
import { marginHint } from './margin';
import { PricingType } from './types';

const base = { name: 'Latte', basePrice: 120, sortOrder: 0 };

describe('parseCostPrice', () => {
  it('treats blank as unknown (null), not zero', () => {
    expect(parseCostPrice('')).toEqual({ ok: true, value: null });
    expect(parseCostPrice('   ')).toEqual({ ok: true, value: null });
  });
  it('accepts zero and up to 999999.99 with 2 decimals', () => {
    expect(parseCostPrice('0')).toEqual({ ok: true, value: 0 });
    expect(parseCostPrice('45.5')).toEqual({ ok: true, value: 45.5 });
    expect(parseCostPrice('999999.99')).toEqual({ ok: true, value: 999999.99 });
  });
  it('rejects negatives, too large, 3 decimals and non-numbers', () => {
    for (const bad of ['-1', '1000000', '1.234', 'abc']) expect(parseCostPrice(bad).ok).toBe(false);
  });
});

describe('item schemas cost rules', () => {
  it('cost is optional on create and update', () => {
    expect(createItemSchema.safeParse({ ...base, pricingType: PricingType.Unit }).success).toBe(true);
    expect(createItemSchema.safeParse({ ...base, pricingType: PricingType.Unit, costPrice: '' }).success).toBe(true);
    expect(updateItemSchema.safeParse({ ...base, isActive: true, costPrice: '30' }).success).toBe(true);
  });
  it('rejects a negative or oversized cost', () => {
    expect(updateItemSchema.safeParse({ ...base, isActive: true, costPrice: '-5' }).success).toBe(false);
    expect(updateItemSchema.safeParse({ ...base, isActive: true, costPrice: '1000000' }).success).toBe(false);
  });
});

describe('marginHint', () => {
  it('shows margin and percent from integer cents', () => {
    expect(marginHint(120, '45')).toBe('Margin ₱75.00 (63%)');
    expect(marginHint(0.1, '0.07')).toBe('Margin ₱0.03 (30%)');
  });
  it('says cost unknown when cost is blank or invalid', () => {
    expect(marginHint(120, '')).toBe('Cost unknown');
    expect(marginHint(120, 'x')).toBe('Cost unknown');
  });
  it('shows nothing useful for zero or invalid price', () => {
    expect(marginHint(0, '10')).toBe('Set a price to see the margin');
    expect(marginHint(Number.NaN, '10')).toBe('Set a price to see the margin');
  });
  it('shows a negative margin when cost exceeds price', () => {
    expect(marginHint(50, '60')).toBe('Margin -₱10.00 (-20%)');
  });
});
