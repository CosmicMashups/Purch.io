import { describe, expect, it } from 'vitest';
import { makeCart, makeLine } from '../../test/pos';
import { IDLE_STATE, stateForCart, stateForPayment, stateForReceipt } from './channel';

const cart = makeCart({
  subtotal: 300,
  totalAmount: 270,
  itemPromoDiscountAmount: 20,
  promoDiscountAmount: 10,
  promoCode: 'SAVE10',
  lines: [makeLine({ id: 'a', itemName: 'Latte', quantity: 2, lineTotal: 300 })],
});

describe('stateForCart', () => {
  it('shows welcome for an empty or missing cart', () => {
    expect(stateForCart(makeCart())).toEqual(IDLE_STATE);
    expect(stateForCart(null)).toEqual(IDLE_STATE);
    expect(stateForCart(undefined)).toEqual(IDLE_STATE);
  });

  it('copies the server figures and does no arithmetic of its own', () => {
    const s = stateForCart(cart);
    expect(s.mode).toBe('cart');
    expect(s.total).toBe(270);
    expect(s.subtotal).toBe(300);
    expect(s.lines).toEqual([{ name: 'Latte', quantity: 2, lineTotal: 300 }]);
  });

  it('lists each saving the server reported, and only those', () => {
    expect(stateForCart(cart).savings).toEqual([
      { label: 'Item promotions', amount: 20 },
      { label: 'Promo code SAVE10', amount: 10 },
    ]);
  });
});

describe('stateForPayment', () => {
  it('is the same order, marked as paying', () => {
    expect(stateForPayment(cart)).toMatchObject({ mode: 'payment', total: 270 });
  });

  it('stays on welcome when there is nothing to pay', () => {
    expect(stateForPayment(makeCart())).toEqual(IDLE_STATE);
  });
});

describe('stateForReceipt', () => {
  it('shows the change given', () => {
    const receipt = makeCart({
      ...cart,
      receiptNumber: 55,
      payments: [{ id: 'p', method: 0, status: 1, amount: 270, amountTendered: 300, changeGiven: 30 }],
    });
    expect(stateForReceipt(receipt)).toMatchObject({ mode: 'completed', tendered: 300, change: 30, receiptNumber: 55 });
  });

  it('copes with a sale that has no payment row', () => {
    expect(stateForReceipt(makeCart({ ...cart, payments: [] }))).toMatchObject({ mode: 'completed', tendered: null, change: null });
  });
});
