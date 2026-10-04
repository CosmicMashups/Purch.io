import type { ItemRecipeLine, ReplaceItemRecipeLine } from './types';

/**
 * How one ingredient is used by an item. Used: this much is taken from stock on every sale. Not used: the ingredient is only
 * checked for availability (a sauce, say), so its stock changes only when someone counts it.
 */
export interface RecipeEntry {
  used: boolean;
  /** The quantity text as typed. Only read when `used` is true. */
  quantity: string;
}

/** inventoryItemId -> how it is used. A key being present means the ingredient is chosen. */
export type RecipeSelection = Record<string, RecipeEntry>;

export function selectionFromRecipe(lines: ItemRecipeLine[]): RecipeSelection {
  return Object.fromEntries(
    lines.map((line): [string, RecipeEntry] => [
      line.inventoryItemId,
      line.quantityPerOrder === null ? { used: false, quantity: '' } : { used: true, quantity: String(line.quantityPerOrder) },
    ]),
  );
}

export type RecipeBuildResult =
  | { ok: true; lines: ReplaceItemRecipeLine[] }
  | { ok: false; inventoryItemId: string; message: string };

/**
 * An ingredient that is used up must say how much, and more than nothing. One that is only checked has no quantity (null).
 * The API applies its own rules too and still has the final say.
 */
export function buildRecipeLines(selection: RecipeSelection): RecipeBuildResult {
  const lines: ReplaceItemRecipeLine[] = [];
  for (const [inventoryItemId, entry] of Object.entries(selection)) {
    if (!entry.used) {
      lines.push({ inventoryItemId, quantityPerOrder: null });
      continue;
    }
    const text = entry.quantity.trim();
    if (text === '') return { ok: false, inventoryItemId, message: "Enter how much is used per order, or choose Just check it's in stock" };
    const quantity = Number(text);
    if (!Number.isFinite(quantity)) return { ok: false, inventoryItemId, message: 'Enter a number' };
    if (quantity < 0) return { ok: false, inventoryItemId, message: 'Cannot be negative' };
    if (quantity === 0) return { ok: false, inventoryItemId, message: 'Must be more than 0' };
    lines.push({ inventoryItemId, quantityPerOrder: quantity });
  }
  return { ok: true, lines };
}
