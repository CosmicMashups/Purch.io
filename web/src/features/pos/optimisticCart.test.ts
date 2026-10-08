import { describe, expect, it } from 'vitest';
import { PricingType, TingiMode, type Item, type ItemVariant, type ModifierGroup } from '../catalog/types';
import { previewFor, withPending } from './optimisticCart';
import { KitchenStatus, TransactionStatus, type Transaction } from './types';

const item = (over: Partial<Item> = {}): Item => ({
  id: 'ramen',
  sortOrder: 0,
  name: 'Ramen',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 100,
  imageUrl: null,
  pricingType: PricingType.Unit,
  stockOnHand: 10,
  isActive: true,
  departmentId: null,
  tingiMode: TingiMode.None,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  lowStockThreshold: null,
  isOutOfStock: false,
  ...over,
});

const groups: ModifierGroup[] = [
  { id: 'g', name: 'Extras', allowMultipleSelection: true, isRequired: false, modifiers: [{ id: 'egg', name: 'Egg', priceDelta: 15 }, { id: 'noodles', name: 'Extra noodles', priceDelta: 20 }] },
];
const variant: ItemVariant = { id: 'large', itemId: 'ramen', attributes: { Size: 'Large' }, sku: null, priceOverride: 130, imageUrl: null };

const cart = (over: Partial<Transaction> = {}): Transaction => ({
  id: 't',
  branchId: 'b',
  deviceId: 'd',
  status: TransactionStatus.Open,
  lines: [],
  subtotal: 200,
  discountAmount: 0,
  seniorPwdDiscountApplied: false,
  promoCode: null,
  promoDiscountAmount: 0,
  itemPromoDiscountAmount: 0,
  totalAmount: 200,
  receiptNumber: null,
  orderType: null,
  originatedFromKiosk: false,
  kioskPrepNumber: null,
  kitchenStatus: KitchenStatus.Queued,
  payments: [],
  createdAt: '2026-10-03T02:15:00Z',
  completedAt: null,
  ...over,
});

describe('previewFor', () => {
  it('prices a plain item at its base price', () => {
    expect(previewFor(item(), { itemId: 'ramen', itemVariantId: null, quantity: 1 }, {})).toEqual({ itemId: 'ramen', unitPrice: 100, details: [] });
  });

  it('uses the variant price override and lists its attributes', () => {
    const preview = previewFor(item(), { itemId: 'ramen', itemVariantId: 'large', quantity: 1 }, { variants: [variant] });
    expect(preview).toEqual({ itemId: 'ramen', unitPrice: 130, details: ['Large'] });
  });

  it('adds every chosen modifier price', () => {
    const preview = previewFor(item(), { itemId: 'ramen', itemVariantId: null, quantity: 1, selectedModifierIds: ['egg', 'noodles'] }, { modifierGroups: groups });
    expect(preview?.unitPrice).toBe(135);
    expect(preview?.details).toEqual(['Egg', 'Extra noodles']);
  });

  it('does not guess when a variant or modifier is not loaded', () => {
    expect(previewFor(item(), { itemId: 'ramen', itemVariantId: 'large', quantity: 1 }, {})).toBeUndefined();
    expect(previewFor(item(), { itemId: 'ramen', itemVariantId: null, quantity: 1, selectedModifierIds: ['missing'] }, { modifierGroups: groups })).toBeUndefined();
  });

  it('leaves combos to the server', () => {
    expect(previewFor(item({ pricingType: PricingType.Combo }), { itemId: 'ramen', itemVariantId: null, quantity: 1 }, {})).toBeUndefined();
  });
});

describe('withPending', () => {
  it('returns the cart untouched when nothing pending has a preview price', () => {
    const c = cart();
    expect(withPending(c, [{ key: 'a', label: 'Combo', quantity: 1 }])).toBe(c);
  });

  it('adds pending previews to the subtotal and total', () => {
    const shown = withPending(cart(), [{ key: 'ramen', label: 'Ramen', quantity: 2, unitPrice: 100 }]);
    expect(shown.subtotal).toBe(400);
    expect(shown.totalAmount).toBe(400);
  });

  it('takes the VAT and the Senior/PWD share off pending lines when that discount is on', () => {
    const shown = withPending(cart({ seniorPwdDiscountApplied: true, vatExemptAmount: 21.43, discountAmount: 35.71, totalAmount: 142.86 }), [{ key: 'ramen', label: 'Ramen', quantity: 1, unitPrice: 100 }]);
    expect(shown.subtotal).toBe(300);
    expect(shown.vatExemptAmount).toBe(32.14);
    expect(shown.discountAmount).toBe(53.57);
    expect(shown.totalAmount).toBe(214.29);
  });
});
