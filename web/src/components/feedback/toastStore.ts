import { create } from 'zustand';

export type ToastTone = 'success' | 'error' | 'info';

export interface ToastAction {
  label: string;
  onAction: () => void;
}

export interface Toast {
  id: number;
  tone: ToastTone;
  message: string;
  /** A button on the toast (Undo). Taking it dismisses the toast. */
  action?: ToastAction;
  /** How long it stays, when it is not the tone's default. The countdown on an undo toast follows this. */
  durationMs?: number;
}

interface ToastState {
  toasts: Toast[];
  push: (tone: ToastTone, message: string, options?: { action?: ToastAction; durationMs?: number }) => void;
  dismiss: (id: number) => void;
}

/** How long a change made from a row menu (Deactivate, Delete) can be taken back. */
export const UNDO_WINDOW_MS = 5000;

let nextId = 1;
const LIFETIME_MS: Record<ToastTone, number> = { success: 4000, info: 5000, error: 8000 };

export const useToastStore = create<ToastState>((set, get) => ({
  toasts: [],
  push: (tone, message, options) => {
    const id = nextId++;
    set((s) => ({ toasts: [...s.toasts.slice(-3), { id, tone, message, ...options }] }));
    setTimeout(() => get().dismiss(id), options?.durationMs ?? LIFETIME_MS[tone]);
  },
  dismiss: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}));

export const toast = {
  success: (message: string) => useToastStore.getState().push('success', message),
  error: (message: string) => useToastStore.getState().push('error', message),
  info: (message: string) => useToastStore.getState().push('info', message),
  /** A change that has already happened but can be taken back for a few seconds, with a visible countdown. */
  undoable: (message: string, onUndo: () => void, durationMs = UNDO_WINDOW_MS) =>
    useToastStore.getState().push('success', message, { action: { label: 'Undo', onAction: onUndo }, durationMs }),
};
