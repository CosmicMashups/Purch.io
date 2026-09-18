import { create } from 'zustand';

// Local UI-only state for the catalog module. Server data always lives in TanStack Query —
// this store never holds items/categories/etc.
interface CatalogUiState {
  isAttachModifierGroupOpen: boolean;
  setAttachModifierGroupOpen: (open: boolean) => void;
  confirmDialog: { title: string; description?: string; onConfirm: () => void } | null;
  openConfirmDialog: (dialog: CatalogUiState['confirmDialog']) => void;
  closeConfirmDialog: () => void;
}

export const useCatalogUiStore = create<CatalogUiState>((set) => ({
  isAttachModifierGroupOpen: false,
  setAttachModifierGroupOpen: (open) => set({ isAttachModifierGroupOpen: open }),
  confirmDialog: null,
  openConfirmDialog: (dialog) => set({ confirmDialog: dialog }),
  closeConfirmDialog: () => set({ confirmDialog: null }),
}));
