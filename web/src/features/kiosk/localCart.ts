import { create } from 'zustand';
import { useAuthStore } from '../../lib/authStore';
import { PricingType, type Item, type ItemComboComponent, type ItemVariant, type ModifierGroup } from '../catalog/types';
import { groupOptions } from '../pos/options';
import { priceCart, type PricingRules } from '../pos/pricing/pricingEngine';
import type { AddLineRequest, ComboSelection, ModifierSelection, Transaction, TransactionLine } from '../pos/types';

/**
 * The kiosk's cart lives entirely on this device until Submit: nothing is posted per tap (E6 design
 * decision). Every add is priced and shown instantly from catalog data already cached on the kiosk;
 * the server re-prices and validates the whole order in one call at Submit (`PlaceKioskOrderAsync`),
 * which is the figure actually charged. This file resolves one add into a priced line and keeps the
 * running cart. The automatic item promos (BOGO/combo/item discounts) are applied with the shared pricing
 * engine on the rules the server publishes for kiosks; without them the cart shows its pre-promo total.
 */

export interface LocalCartLine {
  localId: string;
  itemId: string;
  itemName: string;
  itemVariantId: string | null;
  itemVariantAttributes: Record<string, string>;
  quantity: number;
  unitPrice: number;
  comboSelections: ComboSelection[];
  modifierSelections: ModifierSelection[];
}

export interface ResolveCatalog {
  items: Item[];
  variants?: ItemVariant[];
  comboComponents?: ItemComboComponent[];
  modifierGroups?: ModifierGroup[];
}

const round2 = (value: number) => Math.round(value * 100) / 100;

/** Prices and labels one add from catalog data already on the device. Never throws: an item, variant
 * or modifier the device cannot resolve is simply left out of the price/details rather than blocking
 * the add, since the server validates everything for real at Submit. */
export function resolveAdd(item: Item, request: AddLineRequest, catalog: ResolveCatalog): Omit<LocalCartLine, 'localId'> {
  if (item.pricingType === PricingType.Combo) {
    const slots = catalog.comboComponents ?? [];
    const bySlot = new Map(slots.map((slot) => [slot.id, slot] as const));
    const nameOf = (id: string) => catalog.items.find((candidate) => candidate.id === id)?.name ?? '';

    // The customer's own picks, then the fixed items the server will add itself, so the cart shows the whole deal.
    const picked: ComboSelection[] = (request.comboSelections ?? []).map(({ slotId, selectedItemId }) => ({
      slotId,
      slotLabel: bySlot.get(slotId)?.slotLabel ?? '',
      selectedItemId,
      selectedItemName: nameOf(selectedItemId),
    }));
    const fixed: ComboSelection[] = slots
      .filter((slot) => slot.componentItemId)
      .flatMap((slot) =>
        Array.from({ length: slot.quantity }, () => ({
          slotId: slot.id,
          slotLabel: slot.slotLabel,
          selectedItemId: slot.componentItemId as string,
          selectedItemName: slot.componentItemName ?? nameOf(slot.componentItemId as string),
        })),
      );
    const comboSelections = [...fixed, ...picked];

    // Same rule as the server: each slot's flat charge once, plus the surcharge of every choice that has one.
    const flat = slots.reduce((sum, slot) => sum + (slot.substitutionUpchargeAmount ?? 0), 0);
    const perChoice = picked.reduce((sum, selection) => {
      const entry = bySlot.get(selection.slotId)?.choiceUpcharges?.find((candidate) => candidate.itemId === selection.selectedItemId);
      return sum + (entry?.amount ?? 0);
    }, 0);
    const { modifierSelections, modifierTotal } = resolveModifiers([...(request.selectedModifierIds ?? []), ...(request.selectedCategoryItemIds ?? [])], catalog.modifierGroups);
    return {
      itemId: item.id,
      itemName: item.name,
      itemVariantId: null,
      itemVariantAttributes: {},
      quantity: request.quantity,
      unitPrice: round2(item.basePrice + flat + perChoice + modifierTotal),
      comboSelections,
      modifierSelections,
    };
  }

  let unitPrice = item.basePrice;
  let attributes: Record<string, string> = {};
  if (request.itemVariantId) {
    const variant = catalog.variants?.find((v) => v.id === request.itemVariantId);
    if (variant) {
      unitPrice = variant.priceOverride ?? item.basePrice;
      attributes = variant.attributes;
    }
  }

  const { modifierSelections, modifierTotal } = resolveModifiers([...(request.selectedModifierIds ?? []), ...(request.selectedCategoryItemIds ?? [])], catalog.modifierGroups);
  unitPrice = round2(unitPrice + modifierTotal);

  return {
    itemId: item.id,
    itemName: item.name,
    itemVariantId: request.itemVariantId,
    itemVariantAttributes: attributes,
    quantity: request.quantity,
    unitPrice,
    comboSelections: [],
    modifierSelections,
  };
}

const selectionId = (m: ModifierSelection): string => (m.itemModifierId ?? m.itemId) as string;

function resolveModifiers(modifierIds: string[] | undefined, groups: ModifierGroup[] | undefined): { modifierSelections: ModifierSelection[]; modifierTotal: number } {
  const known = new Map((groups ?? []).flatMap((g) => groupOptions(g).map((o) => [o.id, { option: o, group: g }] as const)));
  const modifierSelections: ModifierSelection[] = [];
  let modifierTotal = 0;
  for (const id of modifierIds ?? []) {
    const found = known.get(id);
    if (!found) continue;
    modifierSelections.push({
      itemModifierId: found.option.kind === 'modifier' ? id : null,
      ...(found.option.kind === 'item' ? { itemId: id } : {}),
      modifierName: found.option.name,
      modifierGroupName: found.group.name,
      priceDelta: found.option.priceDelta,
    });
    modifierTotal += found.option.priceDelta;
  }
  return { modifierSelections, modifierTotal };
}

/**
 * Two lines are the same order when they are the same item, variant and set of modifiers; adding or editing
 * one into the other adds their quantities instead of leaving a duplicate. A combo line never merges, matching
 * the server (each deal keeps its own picks).
 */
export function mergeKey(line: Pick<LocalCartLine, 'itemId' | 'itemVariantId' | 'modifierSelections' | 'comboSelections'>): string | null {
  if (line.comboSelections.length > 0) return null;
  const modifiers = line.modifierSelections.map(selectionId).sort().join(',');
  return `${line.itemId}:${line.itemVariantId ?? ''}:${modifiers}`;
}

function toTransactionLine(line: LocalCartLine, discount?: { discount: number; label: string | null }): TransactionLine {
  return {
    id: line.localId,
    itemId: line.itemId,
    itemName: line.itemName,
    itemVariantId: line.itemVariantId,
    itemVariantAttributes: line.itemVariantAttributes,
    quantity: line.quantity,
    unitPrice: line.unitPrice,
    lineTotal: round2(line.unitPrice * line.quantity),
    promoDiscountAmount: discount?.discount ?? 0,
    appliedPromoLabel: discount?.label ?? null,
    comboSelections: line.comboSelections,
    modifierSelections: line.modifierSelections,
  };
}

/** The cart in the same shape the rest of the app already renders (`Transaction`), so the cart/order-type
 * screens built for the server-backed cart work unchanged against this local one. */
export function toLocalTransaction(lines: LocalCartLine[], rules?: PricingRules): Transaction {
  const priced = priceCart({ lines: lines.map((l) => ({ lineId: l.localId, itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice })), rules });
  const subtotal = round2(priced.grossSubtotal);
  return {
    id: 'local',
    branchId: '',
    deviceId: '',
    status: 0,
    lines: lines.map((line) => toTransactionLine(line, priced.lineDiscounts[line.localId])),
    subtotal,
    discountAmount: 0,
    seniorPwdDiscountApplied: false,
    vatExemptAmount: 0,
    promoCode: null,
    promoDiscountAmount: 0,
    itemPromoDiscountAmount: round2(priced.itemPromoDiscountAmount),
    totalAmount: round2(priced.totalAmount),
    receiptNumber: null,
    orderType: null,
    originatedFromKiosk: true,
    kioskPrepNumber: null,
    kitchenStatus: 0,
    payments: [],
    createdAt: new Date(0).toISOString(),
    completedAt: null,
  };
}

/** How long a cart survives a page reload. A customer who left is gone by then; one who merely refreshed is not. */
export const CART_MAX_AGE_MS = 2 * 60_000;

const STORAGE_KEY = 'purch.kiosk.cart';

function loadLines(): LocalCartLine[] {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const saved = JSON.parse(raw) as { lines?: LocalCartLine[]; savedAt?: number };
    if (!Array.isArray(saved.lines) || typeof saved.savedAt !== 'number' || Date.now() - saved.savedAt > CART_MAX_AGE_MS) return [];
    return saved.lines;
  } catch {
    return [];
  }
}

function saveLines(lines: LocalCartLine[], savedAt: number) {
  try {
    if (lines.length === 0) window.sessionStorage.removeItem(STORAGE_KEY);
    else window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ lines, savedAt }));
  } catch {
    // Storage can be unavailable (private mode, quota). The cart simply will not survive a reload.
  }
}

interface LocalKioskCartState {
  lines: LocalCartLine[];
  lastActivityAt: number;
  add: (line: Omit<LocalCartLine, 'localId'>) => void;
  /** Replaces a line in place with an edited one; if it now matches another line the two become one. */
  replaceLine: (localId: string, line: Omit<LocalCartLine, 'localId'>) => void;
  updateQuantity: (localId: string, quantity: number) => void;
  removeLine: (localId: string) => void;
  clear: () => void;
}

let nextLocalId = 0;
const newLocalId = () => `kiosk-line-${++nextLocalId}-${Date.now().toString(36)}`;

export const useLocalKioskCartStore = create<LocalKioskCartState>((set) => ({
  lines: loadLines(),
  lastActivityAt: Date.now(),
  add: (line) =>
    set((state) => {
      const key = mergeKey(line);
      const existing = key ? state.lines.find((l) => mergeKey(l) === key) : undefined;
      const lines = existing
        ? state.lines.map((l) => (l.localId === existing.localId ? { ...l, quantity: l.quantity + line.quantity } : l))
        : [...state.lines, { ...line, localId: newLocalId() }];
      return { lines, lastActivityAt: Date.now() };
    }),
  replaceLine: (localId, line) =>
    set((state) => {
      const edited = { ...line, localId };
      const key = mergeKey(edited);
      const twin = key ? state.lines.find((l) => l.localId !== localId && mergeKey(l) === key) : undefined;
      const lines = state.lines
        .filter((l) => l.localId !== twin?.localId)
        .map((l) => (l.localId === localId ? { ...edited, quantity: edited.quantity + (twin?.quantity ?? 0) } : l));
      return { lines, lastActivityAt: Date.now() };
    }),
  updateQuantity: (localId, quantity) =>
    set((state) => ({ lines: state.lines.map((l) => (l.localId === localId ? { ...l, quantity } : l)), lastActivityAt: Date.now() })),
  removeLine: (localId) => set((state) => ({ lines: state.lines.filter((l) => l.localId !== localId), lastActivityAt: Date.now() })),
  clear: () => set({ lines: [], lastActivityAt: Date.now() }),
}));

// Whatever changes the cart is written through, so a reload picks the order back up where it was.
useLocalKioskCartStore.subscribe((state, previous) => {
  if (state.lines !== previous.lines) saveLines(state.lines, state.lastActivityAt);
});

useAuthStore.subscribe((state, previous) => {
  if (previous.accessToken && !state.accessToken) useLocalKioskCartStore.getState().clear();
});
