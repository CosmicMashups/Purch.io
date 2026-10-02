import { beforeEach, describe, expect, it } from 'vitest';
import { PricingType, type Item } from '../catalog/types';
import type { AddLineRequest } from '../pos/types';
import { resolveAdd, toLocalTransaction, useLocalKioskCartStore } from './localCart';

function makeItem(overrides: Partial<Item> = {}): Item {
  return {
    id: 'item-1',
    name: 'Iced Latte',
    sku: null,
    barcode: null,
    categoryId: null,
    basePrice: 150,
    imageUrl: null,
    pricingType: PricingType.Unit,
    stockOnHand: 0,
    isActive: true,
    departmentId: null,
    tingiMode: 0,
    packagedSize: null,
    tingiIncrementStep: null,
    tingiAllowedSizes: [],
    serviceDurationMinutes: null,
    lowStockThreshold: null,
    isOutOfStock: false,
    ...overrides,
  };
}

describe('resolveAdd', () => {
  it('prices a plain unit item at its base price', () => {
    const line = resolveAdd(makeItem(), { itemId: 'item-1', itemVariantId: null, quantity: 2 }, { items: [] });
    expect(line.unitPrice).toBe(150);
    expect(line.quantity).toBe(2);
  });

  it('prefers the variant override price and carries its attributes', () => {
    const item = makeItem({ pricingType: PricingType.VariantMatrix });
    const line = resolveAdd(
      item,
      { itemId: 'item-1', itemVariantId: 'v1', quantity: 1 },
      { items: [], variants: [{ id: 'v1', itemId: 'item-1', attributes: { Size: 'Large' }, sku: null, priceOverride: 180, imageUrl: null }] },
    );
    expect(line.unitPrice).toBe(180);
    expect(line.itemVariantAttributes).toEqual({ Size: 'Large' });
  });

  it('adds modifier price deltas on top of the base price', () => {
    const line = resolveAdd(makeItem(), { itemId: 'item-1', itemVariantId: null, quantity: 1, selectedModifierIds: ['m1'] }, {
      items: [],
      modifierGroups: [{ id: 'g1', name: 'Extras', allowMultipleSelection: true, isRequired: false, modifiers: [{ id: 'm1', name: 'Extra shot', priceDelta: 25 }] }],
    });
    expect(line.unitPrice).toBe(175);
    expect(line.modifierSelections).toEqual([{ itemModifierId: 'm1', modifierName: 'Extra shot', modifierGroupName: 'Extras', priceDelta: 25 }]);
  });

  it('prices a combo as its base price plus substitution upcharges', () => {
    const combo = makeItem({ pricingType: PricingType.Combo, basePrice: 200 });
    const request: AddLineRequest = { itemId: 'item-1', itemVariantId: null, quantity: 1, comboSelections: [{ slotId: 's1', selectedItemId: 'fries-large' }] };
    const line = resolveAdd(combo, request, {
      items: [{ ...makeItem({ id: 'fries-large', name: 'Large Fries' }) }],
      comboComponents: [{ id: 's1', itemId: 'item-1', componentCategoryId: 'c1', slotLabel: 'Side', quantity: 1, substitutionUpchargeAmount: 15 }],
    });
    expect(line.unitPrice).toBe(215);
    expect(line.comboSelections).toEqual([{ slotId: 's1', slotLabel: 'Side', selectedItemId: 'fries-large', selectedItemName: 'Large Fries' }]);
  });
});

describe('useLocalKioskCartStore', () => {
  beforeEach(() => useLocalKioskCartStore.setState({ lines: [], lastActivityAt: Date.now() }));

  it('merges a second add of the same plain item into the existing line', () => {
    const add = useLocalKioskCartStore.getState().add;
    add(resolveAdd(makeItem(), { itemId: 'item-1', itemVariantId: null, quantity: 1 }, { items: [] }));
    add(resolveAdd(makeItem(), { itemId: 'item-1', itemVariantId: null, quantity: 1 }, { items: [] }));
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(1);
    expect(useLocalKioskCartStore.getState().lines[0]?.quantity).toBe(2);
  });

  it('never merges combo lines, even for the same item', () => {
    const combo = makeItem({ pricingType: PricingType.Combo });
    const add = useLocalKioskCartStore.getState().add;
    const request: AddLineRequest = { itemId: 'item-1', itemVariantId: null, quantity: 1, comboSelections: [{ slotId: 's1', selectedItemId: 'x' }] };
    add(resolveAdd(combo, request, { items: [], comboComponents: [{ id: 's1', itemId: 'item-1', componentCategoryId: 'c1', slotLabel: 'Side', quantity: 1, substitutionUpchargeAmount: 0 }] }));
    add(resolveAdd(combo, request, { items: [], comboComponents: [{ id: 's1', itemId: 'item-1', componentCategoryId: 'c1', slotLabel: 'Side', quantity: 1, substitutionUpchargeAmount: 0 }] }));
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(2);
  });

  it('removes a line by id', () => {
    const { add, removeLine } = useLocalKioskCartStore.getState();
    add(resolveAdd(makeItem(), { itemId: 'item-1', itemVariantId: null, quantity: 1 }, { items: [] }));
    const localId = useLocalKioskCartStore.getState().lines[0]!.localId;
    removeLine(localId);
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(0);
  });
});

describe('toLocalTransaction', () => {
  it('totals the cart from its lines', () => {
    const cart = toLocalTransaction([
      { localId: 'l1', itemId: 'item-1', itemName: 'Iced Latte', itemVariantId: null, itemVariantAttributes: {}, quantity: 2, unitPrice: 150, comboSelections: [], modifierSelections: [] },
    ]);
    expect(cart.subtotal).toBe(300);
    expect(cart.totalAmount).toBe(300);
    expect(cart.lines[0]?.lineTotal).toBe(300);
  });
});

describe('toLocalTransaction with promotions', () => {
  const line = { localId: 'a', itemId: 'latte', itemName: 'Iced Latte', itemVariantId: null, itemVariantAttributes: {}, quantity: 2, unitPrice: 150, comboSelections: [], modifierSelections: [] };

  it('shows the pre-promo total when no rules are known', () => {
    const cart = toLocalTransaction([line]);
    expect(cart.totalAmount).toBe(300);
    expect(cart.itemPromoDiscountAmount).toBe(0);
  });

  it('applies an automatic item discount and labels the line', () => {
    const cart = toLocalTransaction([line], { itemDiscount: [{ itemId: 'latte', discountType: 'percentage', discountValue: 10, isActive: true }] });
    expect(cart.subtotal).toBe(300);
    expect(cart.itemPromoDiscountAmount).toBe(30);
    expect(cart.totalAmount).toBe(270);
    expect(cart.lines[0].appliedPromoLabel).toBe('10% OFF');
  });
});
