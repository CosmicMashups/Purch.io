import type { Item, ItemComboComponent, ItemVariant, Modifier, ModifierGroup } from '../catalog/types';
import type { AddLineRequest } from './types';

/** What has been picked in the options dialog or on the kiosk's Edit Item page. The server prices the result. */
export interface OptionPicks {
  variantId: string | null;
  /** slotId -> the picked item ids, one entry per unit the slot needs. */
  slots: Record<string, string[]>;
  /**
   * modifierGroupId -> picked modifier ids. A group that is absent has not been decided yet; an empty list means
   * the customer deliberately chose none of it ("No sides").
   */
  groups: Record<string, string[]>;
  quantity: number;
}

export interface OptionShape {
  needsVariant: boolean;
  variants: ItemVariant[];
  slots: ItemComboComponent[];
  groups: ModifierGroup[];
}

export interface MissingOptionSettings {
  /**
   * The kiosk makes every modifier group an explicit choice, optional ones included, so a customer never
   * skips "Add drinks" by accident. The till keeps optional groups optional.
   */
  explicitGroups?: boolean;
}

/** "Add drinks" -> "No drinks": the explicit way to say a choice is deliberately left empty. */
export function noneLabel(groupName: string): string {
  const topic = groupName.replace(/^(add|choose|select|pick)\s+/i, '').trim();
  return `No ${topic ? topic.charAt(0).toLowerCase() + topic.slice(1) : 'extras'}`;
}

/** A slot the customer does not choose: it is always the one named item. */
export const isFixedSlot = (slot: ItemComboComponent): boolean => Boolean(slot.componentItemId);

export const isModifierAvailable = (modifier: Modifier): boolean => !modifier.isOutOfStock;

/** One thing a group offers: a modifier of its own or, for a category-linked group, an item of the category. */
export interface GroupOption {
  id: string;
  name: string;
  priceDelta: number;
  soldOut: boolean;
  kind: 'modifier' | 'item';
  imageUrl?: string | null;
}

/** Everything a group offers, its own modifiers first, then the linked category's items that the group has not hidden. */
export function groupOptions(group: ModifierGroup): GroupOption[] {
  return [
    ...group.modifiers.map((modifier): GroupOption => ({ id: modifier.id, name: modifier.name, priceDelta: modifier.priceDelta, soldOut: !isModifierAvailable(modifier), kind: 'modifier' })),
    ...(group.categoryItems ?? [])
      .filter((entry) => !entry.isExcluded)
      .map((entry): GroupOption => ({ id: entry.itemId, name: entry.name, priceDelta: entry.price, soldOut: entry.isOutOfStock, kind: 'item', imageUrl: entry.imageUrl })),
  ];
}

/** The first thing still missing, in plain words, or null when the picks are complete. */
export function missingOption(shape: OptionShape, picks: OptionPicks, settings: MissingOptionSettings = {}): string | null {
  if (shape.needsVariant && !picks.variantId) return 'Choose a variant';
  for (const slot of shape.slots) {
    if (isFixedSlot(slot)) continue;
    const chosen = picks.slots[slot.id] ?? [];
    if (chosen.filter(Boolean).length < slot.quantity) return `Choose ${slot.slotLabel}`;
  }
  for (const group of shape.groups) {
    const chosen = picks.groups[group.id];
    if (group.isRequired && (chosen ?? []).length === 0) return `Choose ${group.name}`;
    if (settings.explicitGroups && chosen === undefined) return `Choose ${group.name}`;
  }
  if (!(picks.quantity > 0)) return 'Quantity must be above zero';
  return null;
}

/**
 * Why this item cannot be ordered at all right now, or null. A required choice with nothing in stock to choose
 * leaves the customer no way to finish, so the item is shown as unavailable instead of trapping them in a dead end.
 */
export function unorderableReason(shape: OptionShape, items: Item[]): string | null {
  for (const group of shape.groups) {
    const options = groupOptions(group);
    if (group.isRequired && options.length > 0 && options.every((option) => option.soldOut)) {
      return `${group.name} is sold out`;
    }
  }
  for (const slot of shape.slots) {
    if (isFixedSlot(slot)) continue;
    if (slotChoices(slot, items).every((choice) => choice.soldOut)) return `${slot.slotLabel} is sold out`;
  }
  return null;
}

/** Toggling a modifier: a single-select group holds one at a time, a multi-select group holds any number. */
export function toggleModifier(group: ModifierGroup, current: string[], modifierId: string): string[] {
  const option = groupOptions(group).find((candidate) => candidate.id === modifierId);
  if (option?.soldOut && !current.includes(modifierId)) return current;
  if (current.includes(modifierId)) return current.filter((id) => id !== modifierId);
  return group.allowMultipleSelection ? [...current, modifierId] : [modifierId];
}

export interface SlotChoice {
  item: Item;
  /** Extra price of this choice on top of the combo, 0 when it is included. */
  upcharge: number;
  soldOut: boolean;
}

/** What the customer can pick for a "choose" slot: the active items of its category, with their surcharge and stock. */
export function slotChoices(slot: ItemComboComponent, items: Item[]): SlotChoice[] {
  const upcharges = new Map((slot.choiceUpcharges ?? []).map((entry) => [entry.itemId, entry.amount] as const));
  return items
    .filter((candidate) => candidate.isActive && candidate.categoryId === slot.componentCategoryId)
    .map((item) => ({ item, upcharge: upcharges.get(item.id) ?? 0, soldOut: item.isOutOfStock }));
}

export function buildAddLine(itemId: string, shape: OptionShape, picks: OptionPicks): AddLineRequest {
  // A fixed slot is filled in by the server, so only the customer's own picks are sent.
  const comboSelections = shape.slots
    .filter((slot) => !isFixedSlot(slot))
    .flatMap((slot) => (picks.slots[slot.id] ?? []).filter(Boolean).map((selectedItemId) => ({ slotId: slot.id, selectedItemId })));
  const pickedIds = Object.values(picks.groups).flat();
  const categoryItemIds = new Set(shape.groups.flatMap((group) => (group.categoryItems ?? []).map((entry) => entry.itemId)));
  const categoryPicks = pickedIds.filter((id) => categoryItemIds.has(id));
  const modifierIds = pickedIds.filter((id) => !categoryItemIds.has(id));
  return {
    itemId,
    itemVariantId: shape.needsVariant ? picks.variantId : null,
    quantity: picks.quantity,
    ...(comboSelections.length > 0 ? { comboSelections } : {}),
    ...(modifierIds.length > 0 ? { selectedModifierIds: modifierIds } : {}),
    ...(categoryPicks.length > 0 ? { selectedCategoryItemIds: categoryPicks } : {}),
  };
}
