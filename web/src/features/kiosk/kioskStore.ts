import { create } from 'zustand';
import type { Transaction } from '../pos/types';
import { CART_MAX_AGE_MS } from './localCart';
import type { DiscountHint, KioskPayment, OrderType } from './tickets';

/** What the customer has chosen on the way to sending the order. */
export interface Checkout {
  orderType: OrderType | null;
  payment: KioskPayment | null;
  /** Only with the discount choice: which discount they will ask the cashier for. */
  discountHint: DiscountHint | null;
}

const EMPTY_CHECKOUT: Checkout = { orderType: null, payment: null, discountHint: null };

interface KioskState {
  /** The order just sent, shown on the confirmation screen. In memory only: a reload returns to the start. */
  submitted: Transaction | null;
  setSubmitted: (order: Transaction | null) => void;

  checkout: Checkout;
  setOrderType: (orderType: OrderType) => void;
  setPayment: (payment: KioskPayment, discountHint?: DiscountHint | null) => void;
  /** Forgets the choices (and the pending order id) for the next customer. */
  resetCheckout: () => void;

  /**
   * The idempotency key for sending this exact order. It is kept across retries so a lost response never
   * sends the order twice, and replaced as soon as anything about the order changes.
   */
  orderIdFor: (fingerprint: string) => string;
}

const STORAGE_KEY = 'purch.kiosk.checkout';

interface Saved {
  checkout: Checkout;
  order: { id: string; fingerprint: string } | null;
  savedAt: number;
}

function load(): Saved | null {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const saved = JSON.parse(raw) as Saved;
    return typeof saved.savedAt === 'number' && Date.now() - saved.savedAt <= CART_MAX_AGE_MS ? saved : null;
  } catch {
    return null;
  }
}

function save(saved: Saved) {
  try {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(saved));
  } catch {
    // Not remembering the choices only means they are asked again after a reload.
  }
}

const restored = load();
let pendingOrder = restored?.order ?? null;

export const useKioskStore = create<KioskState>((set, get) => ({
  submitted: null,
  setSubmitted: (submitted) => set({ submitted }),

  checkout: restored?.checkout ?? EMPTY_CHECKOUT,
  setOrderType: (orderType) => set((state) => ({ checkout: { ...state.checkout, orderType } })),
  setPayment: (payment, discountHint = null) => set((state) => ({ checkout: { ...state.checkout, payment, discountHint: payment === 'discount' ? discountHint : null } })),
  resetCheckout: () => {
    pendingOrder = null;
    try {
      window.sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Nothing stored to forget.
    }
    set({ checkout: EMPTY_CHECKOUT });
  },

  orderIdFor: (fingerprint) => {
    if (pendingOrder?.fingerprint !== fingerprint) {
      pendingOrder = { id: crypto.randomUUID(), fingerprint };
      save({ checkout: get().checkout, order: pendingOrder, savedAt: Date.now() });
    }
    return pendingOrder.id;
  },
}));

useKioskStore.subscribe((state, previous) => {
  if (state.checkout !== previous.checkout && state.checkout !== EMPTY_CHECKOUT) save({ checkout: state.checkout, order: pendingOrder, savedAt: Date.now() });
});
