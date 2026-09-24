import { create } from 'zustand';
import type { Transaction } from '../pos/types';

interface KioskState {
  /** The order just sent, shown on the confirmation screen. In memory only: a reload returns to the start. */
  submitted: Transaction | null;
  setSubmitted: (order: Transaction | null) => void;
}

export const useKioskStore = create<KioskState>((set) => ({ submitted: null, setSubmitted: (submitted) => set({ submitted }) }));
