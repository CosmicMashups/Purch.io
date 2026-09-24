import type { ItemComboComponent, ItemVariant, ModifierGroup } from '../catalog/types';
import type { AddLineRequest } from './types';

/** What the cashier has picked in the options dialog. The server prices the result. */
export interface OptionPicks {
  variantId: string | null;
  /** slotId -> the picked item ids, one entry per unit the slot needs. */
  slots: Record<string, string[]>;
  /** modifierGroupId -> picked modifier ids. */
  groups: Record<string, string[]>;
  quantity: number;
}

export interface OptionShape {
  needsVariant: boolean;
  variants: ItemVariant[];
  slots: ItemComboComponent[];
  groups: ModifierGroup[];
}

/** The first thing still missing, in plain words, or null when the picks are complete. */
export function missingOption(shape: OptionShape, picks: OptionPicks): string | null {
  if (shape.needsVariant && !picks.variantId) return 'Choose a variant';
  for (const slot of shape.slots) {
    const chosen = picks.slots[slot.id] ?? [];
    if (chosen.filter(Boolean).length < slot.quantity) return `Choose ${slot.slotLabel}`;
  }
  for (const group of shape.groups) {
    if (group.isRequired && (picks.groups[group.id] ?? []).length === 0) return `Choose ${group.name}`;
  }
  if (!(picks.quantity > 0)) return 'Quantity must be above zero';
  return null;
}

/** Toggling a modifier: a single-select group holds one at a time, a multi-select group holds any number. */
export function toggleModifier(group: ModifierGroup, current: string[], modifierId: string): string[] {
  if (current.includes(modifierId)) return current.filter((id) => id !== modifierId);
  return group.allowMultipleSelection ? [...current, modifierId] : [modifierId];
}

export function buildAddLine(itemId: string, shape: OptionShape, picks: OptionPicks): AddLineRequest {
  const comboSelections = shape.slots.flatMap((slot) =>
    (picks.slots[slot.id] ?? []).filter(Boolean).map((selectedItemId) => ({ slotId: slot.id, selectedItemId })),
  );
  const modifierIds = Object.values(picks.groups).flat();
  return {
    itemId,
    itemVariantId: shape.needsVariant ? picks.variantId : null,
    quantity: picks.quantity,
    ...(comboSelections.length > 0 ? { comboSelections } : {}),
    ...(modifierIds.length > 0 ? { selectedModifierIds: modifierIds } : {}),
  };
}
