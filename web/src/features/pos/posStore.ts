import { create } from 'zustand';
import type { Transaction } from './types';

interface PosState {
  /** The sale just completed, shown on the receipt screen. Held only in memory: reloading returns to Sell. */
  receipt: Transaction | null;
  showReceipt: (transaction: Transaction) => void;
  clearReceipt: () => void;
}

export const usePosStore = create<PosState>((set) => ({
  receipt: null,
  showReceipt: (receipt) => set({ receipt }),
  clearReceipt: () => set({ receipt: null }),
}));
