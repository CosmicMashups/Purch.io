import { create } from 'zustand';
import { useAuthStore } from '../../lib/authStore';
import { PricingType, type Item, type ItemComboComponent, type ItemVariant, type ModifierGroup } from '../catalog/types';
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
    const bySlot = new Map((catalog.comboComponents ?? []).map((slot) => [slot.id, slot] as const));
    const comboSelections: ComboSelection[] = (request.comboSelections ?? []).map(({ slotId, selectedItemId }) => {
      const slot = bySlot.get(slotId);
      const selected = catalog.items.find((candidate) => candidate.id === selectedItemId);
      return { slotId, slotLabel: slot?.slotLabel ?? '', selectedItemId, selectedItemName: selected?.name ?? '' };
    });
    const upcharge = comboSelections.reduce((sum, selection) => sum + (bySlot.get(selection.slotId)?.substitutionUpchargeAmount ?? 0), 0);
    const { modifierSelections, modifierTotal } = resolveModifiers(request.selectedModifierIds, catalog.modifierGroups);
    return {
      itemId: item.id,
      itemName: item.name,
      itemVariantId: null,
      itemVariantAttributes: {},
      quantity: request.quantity,
      unitPrice: round2(item.basePrice + upcharge + modifierTotal),
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

  const { modifierSelections, modifierTotal } = resolveModifiers(request.selectedModifierIds, catalog.modifierGroups);
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

function resolveModifiers(modifierIds: string[] | undefined, groups: ModifierGroup[] | undefined): { modifierSelections: ModifierSelection[]; modifierTotal: number } {
  const known = new Map((groups ?? []).flatMap((g) => g.modifiers.map((m) => [m.id, { modifier: m, group: g }] as const)));
  const modifierSelections: ModifierSelection[] = [];
  let modifierTotal = 0;
  for (const id of modifierIds ?? []) {
    const found = known.get(id);
    if (!found) continue;
    modifierSelections.push({ itemModifierId: id, modifierName: found.modifier.name, modifierGroupName: found.group.name, priceDelta: found.modifier.priceDelta });
    modifierTotal += found.modifier.priceDelta;
  }
  return { modifierSelections, modifierTotal };
}

/** Same item, same variant, no modifiers on either side: the server merges this case too (see
 * TransactionService.StageLineAsync); a combo line never merges, matching the server exactly. */
function mergeKey(line: Pick<LocalCartLine, 'itemId' | 'itemVariantId' | 'modifierSelections' | 'comboSelections'>): string | null {
  if (line.comboSelections.length > 0 || line.modifierSelections.length > 0) return null;
  return `${line.itemId}:${line.itemVariantId ?? ''}`;
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
  };
}

interface LocalKioskCartState {
  lines: LocalCartLine[];
  lastActivityAt: number;
  add: (line: Omit<LocalCartLine, 'localId'>) => void;
  updateQuantity: (localId: string, quantity: number) => void;
  removeLine: (localId: string) => void;
  clear: () => void;
}

let nextLocalId = 0;

export const useLocalKioskCartStore = create<LocalKioskCartState>((set) => ({
  lines: [],
  lastActivityAt: Date.now(),
  add: (line) =>
    set((state) => {
      const key = mergeKey(line);
      const existing = key ? state.lines.find((l) => mergeKey(l) === key) : undefined;
      const lines = existing
        ? state.lines.map((l) => (l.localId === existing.localId ? { ...l, quantity: l.quantity + line.quantity } : l))
        : [...state.lines, { ...line, localId: `kiosk-line-${++nextLocalId}` }];
      return { lines, lastActivityAt: Date.now() };
    }),
  updateQuantity: (localId, quantity) =>
    set((state) => ({ lines: state.lines.map((l) => (l.localId === localId ? { ...l, quantity } : l)), lastActivityAt: Date.now() })),
  removeLine: (localId) => set((state) => ({ lines: state.lines.filter((l) => l.localId !== localId), lastActivityAt: Date.now() })),
  clear: () => set({ lines: [], lastActivityAt: Date.now() }),
}));

useAuthStore.subscribe((state, previous) => {
  if (previous.accessToken && !state.accessToken) useLocalKioskCartStore.getState().clear();
});
