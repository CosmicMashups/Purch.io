import { describe, expect, it } from 'vitest';
import { bogoSchema, comboSchema, itemDiscountSchema, promoCodeSchema } from './schemas';

const bogoBase = {
  name: 'Latte B1T1',
  triggerItemId: 'a',
  triggerQuantity: 1,
  freeItemId: 'a',
  freeQuantity: 1,
  startsAt: '',
  endsAt: '',
  isActive: true,
};

function firstMessage(result: { success: boolean; error?: { issues: { message: string }[] } }): string | undefined {
  return result.success ? undefined : result.error?.issues[0].message;
}

describe('bogoSchema', () => {
  it('accepts a valid rule, including trigger and free item being the same', () => {
    expect(bogoSchema.safeParse(bogoBase).success).toBe(true);
  });

  it('requires whole quantities of at least 1', () => {
    expect(firstMessage(bogoSchema.safeParse({ ...bogoBase, triggerQuantity: 0 }))).toBe('Must be at least 1');
    expect(firstMessage(bogoSchema.safeParse({ ...bogoBase, freeQuantity: 1.5 }))).toBe('Enter a whole number');
    expect(firstMessage(bogoSchema.safeParse({ ...bogoBase, freeQuantity: NaN }))).toBe('Enter a whole number');
  });

  it('rejects an end before the start', () => {
    const result = bogoSchema.safeParse({ ...bogoBase, startsAt: '2026-09-24T10:00', endsAt: '2026-09-24T09:00' });
    expect(firstMessage(result)).toBe('End must be after the start');
  });
});

describe('comboSchema', () => {
  const base = { name: 'Meal deal', itemAId: 'a', itemBId: 'b', comboPrice: 150, startsAt: '', endsAt: '', isActive: true };

  it('accepts a valid combo', () => {
    expect(comboSchema.safeParse(base).success).toBe(true);
  });

  it('requires two different items and a non-negative price', () => {
    expect(firstMessage(comboSchema.safeParse({ ...base, itemBId: 'a' }))).toBe('Choose two different items');
    expect(firstMessage(comboSchema.safeParse({ ...base, comboPrice: -1 }))).toBe('Price cannot be negative');
  });
});

describe('itemDiscountSchema', () => {
  const base = { name: 'Happy hour', itemId: 'a', discountType: 0, discountValue: 10, startsAt: '', endsAt: '', isActive: true };

  it('accepts a percentage up to 100', () => {
    expect(itemDiscountSchema.safeParse({ ...base, discountValue: 100 }).success).toBe(true);
    expect(firstMessage(itemDiscountSchema.safeParse({ ...base, discountValue: 101 }))).toBe('A percentage cannot be more than 100');
  });

  it('allows a fixed price above 100', () => {
    expect(itemDiscountSchema.safeParse({ ...base, discountType: 2, discountValue: 250 }).success).toBe(true);
  });

  it('requires an amount more than zero', () => {
    expect(firstMessage(itemDiscountSchema.safeParse({ ...base, discountValue: 0 }))).toBe('Must be more than 0');
  });
});

describe('promoCodeSchema', () => {
  const base = { code: 'SUMMER10', discountType: 0, discountValue: 10, expiresAt: '' };

  it('accepts a valid code', () => {
    expect(promoCodeSchema.safeParse(base).success).toBe(true);
  });

  it('rejects a blank code', () => {
    expect(promoCodeSchema.safeParse({ ...base, code: '   ' }).success).toBe(false);
  });

  it('does not offer fixed price as a code discount', () => {
    expect(promoCodeSchema.safeParse({ ...base, discountType: 2 }).success).toBe(false);
  });
});
