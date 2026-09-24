import type { Transaction } from '../../features/pos/types';

export type CustomerDisplayMode = 'idle' | 'cart' | 'payment' | 'completed';

export interface CustomerDisplayLine {
  name: string;
  quantity: number;
  lineTotal: number;
}

/** What the customer may see. Only figures the server already worked out; nothing is calculated here. */
export interface CustomerDisplayState {
  mode: CustomerDisplayMode;
  lines: CustomerDisplayLine[];
  savings: { label: string; amount: number }[];
  subtotal: number;
  total: number;
  tendered: number | null;
  change: number | null;
  receiptNumber: number | null;
}

export const IDLE_STATE: CustomerDisplayState = { mode: 'idle', lines: [], savings: [], subtotal: 0, total: 0, tendered: null, change: null, receiptNumber: null };

function savingsOf(t: Transaction): CustomerDisplayState['savings'] {
  return [
    ...(t.itemPromoDiscountAmount > 0 ? [{ label: 'Item promotions', amount: t.itemPromoDiscountAmount }] : []),
    ...(t.promoDiscountAmount > 0 ? [{ label: t.promoCode ? `Promo code ${t.promoCode}` : 'Promo code', amount: t.promoDiscountAmount }] : []),
    ...(t.discountAmount > 0 ? [{ label: 'Senior / PWD discount', amount: t.discountAmount }] : []),
  ];
}

/** An empty cart shows the welcome screen; otherwise the running order. */
export function stateForCart(cart: Transaction | null | undefined): CustomerDisplayState {
  if (!cart || cart.lines.length === 0) return IDLE_STATE;
  return {
    ...IDLE_STATE,
    mode: 'cart',
    lines: cart.lines.map((l) => ({ name: l.itemName, quantity: l.quantity, lineTotal: l.lineTotal })),
    savings: savingsOf(cart),
    subtotal: cart.subtotal,
    total: cart.totalAmount,
  };
}

export function stateForPayment(cart: Transaction | null | undefined): CustomerDisplayState {
  const base = stateForCart(cart);
  return base.mode === 'idle' ? base : { ...base, mode: 'payment' };
}

export function stateForReceipt(receipt: Transaction): CustomerDisplayState {
  const payment = receipt.payments[0];
  return {
    ...stateForCart(receipt),
    mode: 'completed',
    total: receipt.totalAmount,
    tendered: payment?.amountTendered ?? null,
    change: payment?.changeGiven ?? null,
    receiptNumber: receipt.receiptNumber,
  };
}

export const CUSTOMER_DISPLAY_PATH = '/customer-display';
const CHANNEL_NAME = 'purch.customer-display';

type Message = { type: 'state'; state: CustomerDisplayState } | { type: 'request' };

let last: CustomerDisplayState = IDLE_STATE;
let responder: BroadcastChannel | null = null;

function open(): BroadcastChannel | null {
  return typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel(CHANNEL_NAME);
}

/**
 * Sent from the till to a second window on the same browser. A window opened later asks for the
 * current state, so it never starts blank.
 */
export function publishCustomerDisplay(state: CustomerDisplayState): void {
  last = state;
  if (!responder) {
    responder = open();
    if (responder) {
      const channel = responder;
      channel.onmessage = (event: MessageEvent<Message>) => {
        if (event.data.type === 'request') channel.postMessage({ type: 'state', state: last } satisfies Message);
      };
    }
  }
  responder?.postMessage({ type: 'state', state } satisfies Message);
}

/** The display window's side: get told about every change, and ask for the current one on start. */
export function subscribeCustomerDisplay(onState: (state: CustomerDisplayState) => void): () => void {
  const channel = open();
  if (!channel) return () => undefined;
  channel.onmessage = (event: MessageEvent<Message>) => {
    if (event.data.type === 'state') onState(event.data.state);
  };
  channel.postMessage({ type: 'request' } satisfies Message);
  return () => channel.close();
}

export function customerDisplaySupported(): boolean {
  return typeof BroadcastChannel !== 'undefined';
}
