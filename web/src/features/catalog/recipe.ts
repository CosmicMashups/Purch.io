import type { ItemRecipeLine, ReplaceItemRecipeLine } from './types';

/** inventoryItemId -> the quantity text as typed. A key being present means the ingredient is checked. */
export type RecipeSelection = Record<string, string>;

export function selectionFromRecipe(lines: ItemRecipeLine[]): RecipeSelection {
  return Object.fromEntries(lines.map((line) => [line.inventoryItemId, line.quantityPerOrder === null ? '' : String(line.quantityPerOrder)]));
}

export type RecipeBuildResult =
  | { ok: true; lines: ReplaceItemRecipeLine[] }
  | { ok: false; inventoryItemId: string; message: string };

/**
 * Blank means "just check availability" (null). Anything typed must be a number that is not
 * negative, which is the same rule the API applies; the API still has the final say.
 */
export function buildRecipeLines(selection: RecipeSelection): RecipeBuildResult {
  const lines: ReplaceItemRecipeLine[] = [];
  for (const [inventoryItemId, text] of Object.entries(selection)) {
    const trimmed = text.trim();
    if (trimmed === '') {
      lines.push({ inventoryItemId, quantityPerOrder: null });
      continue;
    }
    const quantity = Number(trimmed);
    if (!Number.isFinite(quantity)) return { ok: false, inventoryItemId, message: 'Enter a number' };
    if (quantity < 0) return { ok: false, inventoryItemId, message: 'Cannot be negative' };
    lines.push({ inventoryItemId, quantityPerOrder: quantity });
  }
  return { ok: true, lines };
}
