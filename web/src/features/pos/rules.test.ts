import { describe, expect, it } from 'vitest';
import { makeLine } from '../../test/pos';
import { discountBase, discountMarker, discountPreview, discountSchema, hasManualDiscount, lineNoteSchema, openItemSchema, priceOverrideSchema, remaining, splitChange, validateParts } from './rules';
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
  it('rounds the preview half away from zero on cents, like the server', () => {
    expect(discountPreview('Percent', 10.5, 9.99)).toBe(1.05);
    expect(discountPreview('Percent', 50, 2.01)).toBe(1.01);
    expect(discountPreview('Fixed', 1.005, 9)).toBe(1.01);
  });
  it('uses the gross line total as the base of a line discount, even with one already on', () => {
    const line = makeLine({ id: 'l', itemName: 'A', lineTotal: 200, manualDiscountAmount: 20, manualDiscountKind: 'Fixed', manualDiscountValue: 20 });
    expect(discountBase(makeCart({ lines: [line], subtotal: 200 }), line)).toBe(200);
    expect(discountSchema(200).safeParse({ kind: 'Fixed', value: 220, reason: 'r' }).success).toBe(false);
  });
  it('uses the subtotal after line discounts as the base of a cart discount', () => {
    const lines = [makeLine({ id: 'a', itemName: 'A', lineTotal: 200, manualDiscountAmount: 20 }), makeLine({ id: 'b', itemName: 'B', lineTotal: 100 })];
    const base = discountBase(makeCart({ lines, subtotal: 300 }), null);
    expect(base).toBe(280);
    expect(discountPreview('Percent', 10, base)).toBe(28);
  });
  it('detects a manual discount on the cart or any line', () => {
    expect(hasManualDiscount(makeCart())).toBe(false);
    expect(hasManualDiscount(makeCart({ manualDiscountAmount: 5 }))).toBe(true);
    expect(hasManualDiscount(makeCart({ lines: [makeLine({ id: 'l', itemName: 'A', manualDiscountAmount: 3 })] }))).toBe(true);
  });
});

describe('split tender: remaining and validateParts', () => {
  const cash = (amount: number, amountTendered?: number) => ({ method: 0 as const, amount, amountTendered: amountTendered ?? null });
  const gcash = (amount: number, reference?: string) => ({ method: 3 as const, amount, reference: reference ?? null });
  const utang = (amount: number, customerCreditLedgerId: string | null = 'c1') => ({ method: 5 as const, amount, customerCreditLedgerId });

  it('remaining is the total less the parts, in exact centavos', () => {
    expect(remaining(100.3, [])).toBe(100.3);
    expect(remaining(100.3, [cash(0.1), gcash(0.2)])).toBe(100);
    expect(remaining(0.3, [cash(0.1), gcash(0.2)])).toBe(0);
  });
  it('accepts parts that add up exactly, with cash tendered over its amount', () => {
    expect(validateParts(300, [cash(100, 150), gcash(200, 'REF1')])).toBeNull();
  });
  it('blocks a sum that is not the total', () => {
    expect(validateParts(300, [cash(100), gcash(199.99)])).toMatch(/add up to ₱300\.00/);
    expect(validateParts(300, [cash(100), gcash(200.01)])).toMatch(/add up to ₱300\.00/);
  });
  it('blocks an empty split and more than 10 parts', () => {
    expect(validateParts(10, [])).toMatch(/at least one/);
    expect(validateParts(11, Array.from({ length: 11 }, () => gcash(1)))).toMatch(/at most 10/);
  });
  it('blocks zero, negative and sub-centavo amounts', () => {
    expect(validateParts(10, [cash(0), gcash(10)])).toMatch(/more than zero/);
    expect(validateParts(10, [cash(-5), gcash(15)])).toMatch(/more than zero/);
    expect(validateParts(10, [cash(5.005), gcash(4.995)])).toMatch(/more than zero/);
  });
  it('only lets cash be tendered over its amount, and never under', () => {
    expect(validateParts(100, [cash(50, 40), gcash(50)])).toMatch(/Cash received must cover/);
    expect(validateParts(100, [cash(50), { ...gcash(50), amountTendered: 60 }])).toMatch(/Only cash/);
    expect(validateParts(100, [cash(50), { ...gcash(50), amountTendered: 50 }])).toBeNull();
  });
  it('allows one Utang part, and it needs a customer account', () => {
    expect(validateParts(100, [utang(50), utang(50)])).toMatch(/one Utang/);
    expect(validateParts(100, [cash(50), utang(50, null)])).toMatch(/customer account/);
  });
  it('rejects methods the API does not accept and references over 100 characters', () => {
    expect(validateParts(100, [{ method: 1, amount: 100 }])).toMatch(/isn't available/);
    expect(validateParts(100, [gcash(100, 'x'.repeat(101))])).toMatch(/100 characters/);
  });
  it('totals the change from the cash part', () => {
    expect(splitChange([cash(100, 150), gcash(200)])).toBe(50);
    expect(splitChange([gcash(200)])).toBe(0);
  });
});
