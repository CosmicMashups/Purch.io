import { describe, expect, it } from 'vitest';
import { makeLine } from '../../test/pos';
import { discountMarker, discountPreview, discountSchema, hasManualDiscount, lineNoteSchema, openItemSchema, priceOverrideSchema } from './rules';
import { makeCart } from '../../test/pos';

const ok = (r: { success: boolean }) => r.success;

describe('discountSchema', () => {
  const schema = discountSchema(200);
  it('needs a reason', () => {
    expect(ok(schema.safeParse({ kind: 'Percent', value: 10, reason: '   ' }))).toBe(false);
    expect(ok(schema.safeParse({ kind: 'Percent', value: 10, reason: 'Loyal customer' }))).toBe(true);
  });
  it('caps the reason at 200 characters', () => {
    expect(ok(schema.safeParse({ kind: 'Fixed', value: 5, reason: 'x'.repeat(201) }))).toBe(false);
  });
  it('keeps a percent above 0 and at most 100, to 2 decimal places', () => {
    expect(ok(schema.safeParse({ kind: 'Percent', value: 0, reason: 'r' }))).toBe(false);
    expect(ok(schema.safeParse({ kind: 'Percent', value: 100, reason: 'r' }))).toBe(true);
    expect(ok(schema.safeParse({ kind: 'Percent', value: 100.01, reason: 'r' }))).toBe(false);
    expect(ok(schema.safeParse({ kind: 'Percent', value: 12.345, reason: 'r' }))).toBe(false);
  });
  it('keeps a fixed amount above 0 and no more than the total', () => {
    expect(ok(schema.safeParse({ kind: 'Fixed', value: 200, reason: 'r' }))).toBe(true);
    expect(ok(schema.safeParse({ kind: 'Fixed', value: 200.01, reason: 'r' }))).toBe(false);
    expect(ok(schema.safeParse({ kind: 'Fixed', value: -1, reason: 'r' }))).toBe(false);
  });
});

describe('priceOverrideSchema', () => {
  it('needs a positive price up to 999999.99 and a reason', () => {
    expect(ok(priceOverrideSchema.safeParse({ newUnitPrice: 80, reason: 'Damaged box' }))).toBe(true);
    expect(ok(priceOverrideSchema.safeParse({ newUnitPrice: 0, reason: 'r' }))).toBe(false);
    expect(ok(priceOverrideSchema.safeParse({ newUnitPrice: 1_000_000, reason: 'r' }))).toBe(false);
    expect(ok(priceOverrideSchema.safeParse({ newUnitPrice: 80, reason: '' }))).toBe(false);
  });
});

describe('openItemSchema', () => {
  it('validates name, price, quantity and note', () => {
    const good = { name: 'Gift wrap', unitPrice: 25, quantity: 1, note: '' };
    expect(ok(openItemSchema.safeParse(good))).toBe(true);
    expect(ok(openItemSchema.safeParse({ ...good, name: '  ' }))).toBe(false);
    expect(ok(openItemSchema.safeParse({ ...good, name: 'x'.repeat(81) }))).toBe(false);
    expect(ok(openItemSchema.safeParse({ ...good, unitPrice: 0 }))).toBe(false);
    expect(ok(openItemSchema.safeParse({ ...good, quantity: 0 }))).toBe(false);
    expect(ok(openItemSchema.safeParse({ ...good, quantity: 1.2345 }))).toBe(false);
    expect(ok(openItemSchema.safeParse({ ...good, quantity: 100_000 }))).toBe(false);
    expect(ok(openItemSchema.safeParse({ ...good, note: 'x'.repeat(201) }))).toBe(false);
  });
});

describe('lineNoteSchema', () => {
  it('allows up to 200 characters', () => {
    expect(ok(lineNoteSchema.safeParse('x'.repeat(200)))).toBe(true);
    expect(ok(lineNoteSchema.safeParse('x'.repeat(201)))).toBe(false);
  });
});

describe('markers', () => {
  it('shows kind, value and amount', () => {
    expect(discountMarker(makeLine({ id: 'l', itemName: 'A', manualDiscountKind: 'Percent', manualDiscountValue: 10, manualDiscountAmount: 20 }))).toMatch(/^-10% \(-₱\s?20\.00\)$/);
    expect(discountMarker(makeLine({ id: 'l', itemName: 'A' }))).toBeNull();
  });
  it('previews the resulting amount', () => {
    expect(discountPreview('Percent', 10, 200)).toBe(20);
    expect(discountPreview('Fixed', 15, 200)).toBe(15);
    expect(discountPreview('Percent', NaN, 200)).toBe(0);
  });
  it('detects a manual discount on the cart or any line', () => {
    expect(hasManualDiscount(makeCart())).toBe(false);
    expect(hasManualDiscount(makeCart({ manualDiscountAmount: 5 }))).toBe(true);
    expect(hasManualDiscount(makeCart({ lines: [makeLine({ id: 'l', itemName: 'A', manualDiscountAmount: 3 })] }))).toBe(true);
  });
});
