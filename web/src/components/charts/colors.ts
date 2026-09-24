/** The fixed categorical order. Slot n is always the same hue, and a ninth colour is never generated. */
export const CATEGORY_SLOTS = 8;

export const OTHER_COLOR = 'var(--viz-muted)';

export const slotColor = (index: number): string => `var(--viz-${(index % CATEGORY_SLOTS) + 1})`;

/**
 * Colour follows the entity, never its rank: slots are handed out in the order of the keys, sorted, so filtering or
 * re-sorting a chart never repaints what is left. Pass every key the chart could show, not only the visible ones.
 */
export function assignColors(keys: string[]): Map<string, string> {
  return new Map([...new Set(keys)].sort().map((key, i) => [key, slotColor(i)]));
}
