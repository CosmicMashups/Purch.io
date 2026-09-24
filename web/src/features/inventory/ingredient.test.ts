import { describe, expect, it } from 'vitest';
import { countSchema, ingredientSchema, parseThreshold, receiveSchema } from './ingredient';

const base = { name: 'Espresso Beans', sku: '', baseUnit: 'g', packagingUnit: 'sack', packagingSize: 1000, lowStockThreshold: '', isActive: true };
const first = (r: { success: boolean; error?: { issues: { message: string }[] } }) => (r.success ? null : r.error?.issues[0].message);

describe('ingredientSchema', () => {
  it('accepts a complete ingredient', () => {
    expect(ingredientSchema.safeParse(base).success).toBe(true);
  });

  it('requires a name, both units and a positive packaging size', () => {
    expect(first(ingredientSchema.safeParse({ ...base, name: ' ' }))).toBe('Enter a name');
    expect(first(ingredientSchema.safeParse({ ...base, baseUnit: '' }))).toMatch(/unit you count in/);
    expect(first(ingredientSchema.safeParse({ ...base, packagingSize: 0 }))).toBe('Must be more than 0');
    expect(first(ingredientSchema.safeParse({ ...base, packagingSize: NaN }))).toBe('Enter a number');
  });
});

describe('parseThreshold', () => {
  it('treats blank as no alert level', () => {
    expect(parseThreshold('  ')).toEqual({ ok: true, value: null });
  });

  it('parses numbers and rejects bad input', () => {
    expect(parseThreshold('12.5')).toEqual({ ok: true, value: 12.5 });
    expect(parseThreshold('0')).toEqual({ ok: true, value: 0 });
    expect(parseThreshold('-1')).toEqual({ ok: false, message: 'Cannot be negative' });
    expect(parseThreshold('abc')).toEqual({ ok: false, message: 'Enter a number' });
  });
});

describe('countSchema', () => {
  it('allows a counted zero but not a negative', () => {
    expect(countSchema.safeParse({ branchId: 'b', quantityOnHand: 0 }).success).toBe(true);
    expect(first(countSchema.safeParse({ branchId: 'b', quantityOnHand: -1 }))).toBe('Cannot be negative');
    expect(first(countSchema.safeParse({ branchId: '', quantityOnHand: 3 }))).toBe('Choose a branch');
  });
});

describe('receiveSchema', () => {
  it('requires packages above zero', () => {
    expect(receiveSchema.safeParse({ branchId: 'b', packagesReceived: 2, supplierReference: '' }).success).toBe(true);
    expect(first(receiveSchema.safeParse({ branchId: 'b', packagesReceived: 0, supplierReference: '' }))).toBe('Must be more than 0');
  });
});
